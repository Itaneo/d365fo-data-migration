namespace Dynamics365ImportData.Pipeline;

using Dynamics365ImportData.DependencySorting;
using Dynamics365ImportData.Erp.DataManagementDefinitionGroups;
using Dynamics365ImportData.Fingerprinting;
using Dynamics365ImportData.Persistence.Models;
using Dynamics365ImportData.Services;
using Dynamics365ImportData.Settings;
using Dynamics365ImportData.XmlOutput;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System.Collections.Concurrent;
using System.Diagnostics;

internal class MigrationPipelineService : IMigrationPipelineService
{
    private readonly IErrorFingerprinter _fingerprinter;
    private readonly ILogger<MigrationPipelineService> _logger;
    private readonly IServiceProvider _provider;
    private readonly SourceQueryCollection _queries;
    private readonly SqlToXmlService _sqlToXmlService;
    private readonly int _timeout;

    public MigrationPipelineService(
        SourceQueryCollection queries,
        SqlToXmlService sqlToXmlService,
        IServiceProvider provider,
        IOptions<Dynamics365Settings> settings,
        IErrorFingerprinter fingerprinter,
        ILogger<MigrationPipelineService> logger)
    {
        _queries = queries;
        _sqlToXmlService = sqlToXmlService;
        _provider = provider;
        _fingerprinter = fingerprinter;
        _logger = logger;
        var settingsValue = settings.Value ?? throw new ArgumentNullException(nameof(settings), "The settings are null");
        _timeout = settingsValue.ImportTimeout <= 0 ? 60 : settingsValue.ImportTimeout;
    }

    public async Task<CycleResult> ExecuteAsync(
        PipelineMode mode,
        string[]? entityFilter,
        CancellationToken cancellationToken)
    {
        var overallStopwatch = Stopwatch.StartNew();
        var timestamp = DateTimeOffset.UtcNow;
        var cycleId = $"cycle-{timestamp:yyyy-MM-ddTHHmmss}";

        IXmlOutputFactory factory = ResolveFactory(mode);

        var queriesToProcess = _queries.SortedQueries;

        if (entityFilter is { Length: > 0 })
        {
            var allEntityNames = queriesToProcess
                .SelectMany(level => level)
                .Select(q => q.EntityName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var invalidNames = entityFilter
                .Where(name => !allEntityNames.Contains(name))
                .ToList();

            if (invalidNames.Count > 0)
            {
                throw new EntityValidationException(invalidNames, allEntityNames);
            }

            var filterSet = new HashSet<string>(entityFilter, StringComparer.OrdinalIgnoreCase);
            queriesToProcess = queriesToProcess
                .Select(level => level.Where(q => filterSet.Contains(q.EntityName)).ToList())
                .Where(level => level.Count > 0)
                .ToList();

            _logger.LogInformation("Processing {Count} of {Total} entities: {EntityNames}",
                filterSet.Count, allEntityNames.Count,
                string.Join(", ", entityFilter));
        }

        var entityResults = new ConcurrentDictionary<string, EntityResult>();

        var (parts, succeeded, failed) = await RunQueriesWithDependenciesAsync(
            factory, queriesToProcess, entityResults, cancellationToken);

        overallStopwatch.Stop();

        var orderedResults = entityResults.Values.OrderBy(r => r.EntityName).ToList();

        var summary = new CycleSummary
        {
            TotalEntities = entityResults.Values.Count,
            Succeeded = entityResults.Values.Count(r => r.Status == EntityStatus.Success),
            Failed = entityResults.Values.Count(r => r.Status == EntityStatus.Failed),
            Warnings = entityResults.Values.Count(r => r.Status == EntityStatus.Warning),
            Skipped = entityResults.Values.Count(r => r.Status == EntityStatus.Skipped),
            TotalDurationMs = overallStopwatch.ElapsedMilliseconds
        };

        _logger.LogInformation(
            "Migration cycle {CycleId} complete: {Succeeded} succeeded, {Failed} failed, {Total} total in {DurationMs}ms",
            cycleId, summary.Succeeded, summary.Failed, summary.TotalEntities, summary.TotalDurationMs);

        return new CycleResult
        {
            Command = mode.ToString(),
            TotalEntities = parts.Count,
            Succeeded = succeeded,
            Failed = failed,
            CycleId = cycleId,
            Timestamp = timestamp,
            EntitiesRequested = entityFilter ?? ["all"],
            Results = orderedResults,
            Summary = summary,
            TotalDurationMs = overallStopwatch.ElapsedMilliseconds
        };
    }

    private IXmlOutputFactory ResolveFactory(PipelineMode mode)
    {
        return mode switch
        {
            PipelineMode.File => _provider.GetRequiredService<XmlFileOutputFactory>(),
            PipelineMode.Package => _provider.GetRequiredService<XmlPackageFileOutputFactory>(),
            PipelineMode.D365 => _provider.GetRequiredService<XmlD365FnoOutputFactory>(),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported pipeline mode")
        };
    }

    private async Task<(ConcurrentBag<IXmlOutputPart> parts, int succeeded, int failed)> RunQueriesWithDependenciesAsync(
        IXmlOutputFactory factory,
        List<List<SourceQueryItem>> queries,
        ConcurrentDictionary<string, EntityResult> entityResults,
        CancellationToken cancellationToken)
    {
        ConcurrentBag<IXmlOutputPart> parts = new();
        int totalSucceeded = 0;
        int totalFailed = 0;

        foreach (List<SourceQueryItem> tasks in queries)
        {
            ConcurrentBag<IXmlOutputPart> running = new();
            await Parallel.ForEachAsync(
                tasks,
                cancellationToken,
                async (source, token) =>
                {
                    var entityStopwatch = Stopwatch.StartNew();
                    var entityResult = new EntityResult
                    {
                        EntityName = source.EntityName,
                        DefinitionGroupId = source.DefinitionGroupId,
                        Status = EntityStatus.Success,
                        RecordCount = 0
                    };
                    try
                    {
                        IEnumerable<IXmlOutputPart> list = await _sqlToXmlService
                            .ExportToOutput(source, factory, token);
                        foreach (IXmlOutputPart part in list)
                        {
                            parts.Add(part);
                            running.Add(part);
                        }
                    }
                    catch (Exception ex)
                    {
                        var error = new EntityError
                        {
                            Message = ex.Message,
                            Fingerprint = _fingerprinter.ComputeFingerprint(source.EntityName, ex.Message),
                            Category = ErrorCategory.Technical
                        };
                        entityResult.Status = EntityStatus.Failed;
                        entityResult.Errors.Add(error);
                        _logger.LogError(ex, "Entity {EntityName} failed during processing", source.EntityName);
                    }
                    finally
                    {
                        entityStopwatch.Stop();
                        entityResult.DurationMs = entityStopwatch.ElapsedMilliseconds;
                        entityResults.TryAdd(source.EntityName, entityResult);
                    }
                });
            var (hasFailed, succeeded, failed) = await CheckTasksStatus(running.ToArray(), cancellationToken);
            totalSucceeded += succeeded;
            totalFailed += failed;
            if (hasFailed)
            {
                _logger.LogError("Tasks in error state detected in current dependency level");
            }
        }
        return (parts, totalSucceeded, totalFailed);
    }

    private async Task<(bool hasFailed, int succeeded, int failed)> CheckTasksStatus(
        IEnumerable<IXmlOutputPart> running, CancellationToken cancellationToken)
    {
        IEnumerable<IXmlOutputPart> tasks = running.Reverse();
        bool anyTerminalState = false;
        bool anyFailed = false;
        int succeeded = 0;
        int failed = 0;

        while (true)
        {
            List<IXmlOutputPart> remaining = new();
            foreach (IXmlOutputPart? t in tasks)
            {
                ExecutionStatus executionStatus = await t.GetStateAsync(cancellationToken);
                switch (executionStatus)
                {
                    case ExecutionStatus.Succeeded:
                        _logger.LogInformation("Task {PartName} Succeded", t.PartName);
                        anyTerminalState = true;
                        succeeded++;
                        break;

                    case ExecutionStatus.Failed:
                        _logger.LogError("Task {PartName} Failed", t.PartName);
                        anyTerminalState = true;
                        anyFailed = true;
                        failed++;
                        break;

                    case ExecutionStatus.PartiallySucceeded:
                        _logger.LogError("Task {PartName} partially succeeded", t.PartName);
                        anyTerminalState = true;
                        anyFailed = true;
                        failed++;
                        break;

                    case ExecutionStatus.Unknown:
                        _logger.LogWarning("Task {PartName} is in an unkown state", t.PartName);
                        remaining.Add(t);
                        break;

                    case ExecutionStatus.Canceled:
                        _logger.LogError("Task {PartName} was cancelled", t.PartName);
                        anyTerminalState = true;
                        anyFailed = true;
                        failed++;
                        break;

                    case ExecutionStatus.Executing:
                        _logger.LogInformation("Task {PartName} is executing", t.PartName);
                        remaining.Add(t);
                        break;

                    case ExecutionStatus.NotRun:
                        _logger.LogInformation("Task {PartName} is not run", t.PartName);
                        remaining.Add(t);
                        break;
                }
                if ((DateTime.UtcNow - t.StartedTime).TotalMinutes > _timeout)
                {
                    _logger.LogError(
                        "Task {PartName} timeout reached. Status={ExecutionStatus}",
                        t.PartName,
                        executionStatus);
                    anyFailed = true;
                    break;
                }
            }
            if (remaining.Count > 0)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    anyFailed = true;
                    failed += remaining.Count;
                    break;
                }
                else
                {
                    await Task.Delay(15000, cancellationToken);
                    tasks = new List<IXmlOutputPart>(remaining);
                }
            }
            else
            {
                break;
            }
        }

        return (anyFailed, succeeded, failed);
    }
}
