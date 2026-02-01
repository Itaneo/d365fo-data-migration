namespace Dynamics365ImportData.Pipeline;

using Dynamics365ImportData.DependencySorting;
using Dynamics365ImportData.Erp.DataManagementDefinitionGroups;
using Dynamics365ImportData.Services;
using Dynamics365ImportData.Settings;
using Dynamics365ImportData.XmlOutput;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System.Collections.Concurrent;

internal class MigrationPipelineService : IMigrationPipelineService
{
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
        ILogger<MigrationPipelineService> logger)
    {
        _queries = queries;
        _sqlToXmlService = sqlToXmlService;
        _provider = provider;
        _logger = logger;
        var settingsValue = settings.Value ?? throw new ArgumentNullException(nameof(settings), "The settings are null");
        _timeout = settingsValue.ImportTimeout <= 0 ? 60 : settingsValue.ImportTimeout;
    }

    public async Task<CycleResult> ExecuteAsync(
        PipelineMode mode,
        string[]? entityFilter,
        CancellationToken cancellationToken)
    {
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

        var (parts, succeeded, failed) = await RunQueriesWithDependenciesAsync(factory, queriesToProcess, cancellationToken);

        return new CycleResult
        {
            Command = mode.ToString(),
            TotalEntities = parts.Count,
            Succeeded = succeeded,
            Failed = failed
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
        IXmlOutputFactory factory, List<List<SourceQueryItem>> queries, CancellationToken cancellationToken)
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
                    IEnumerable<IXmlOutputPart> list = await _sqlToXmlService
                        .ExportToOutput(source, factory, token);
                    foreach (IXmlOutputPart part in list)
                    {
                        parts.Add(part);
                        running.Add(part);
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
                if ((DateTime.UtcNow - t.StartedTime).TotalMinutes > _timeout && anyTerminalState)
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
