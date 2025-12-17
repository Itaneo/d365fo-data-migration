namespace Dynamics365ImportData;

using Cocona;

using Dynamics365ImportData.DependencySorting;
using Dynamics365ImportData.Erp.DataManagementDefinitionGroups;
using Dynamics365ImportData.Services;
using Dynamics365ImportData.Settings;
using Dynamics365ImportData.XmlOutput;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System.Collections.Concurrent;

internal class CommandHandler
{
    private readonly ILogger<CommandHandler> _logger;
    private readonly IServiceProvider _provider;
    private readonly SourceQueryCollection _queries;
    private readonly Dynamics365Settings _settings;
    private readonly SqlToXmlService _sqlToXmlService;
    private readonly int _timeout;

    public CommandHandler(
        IOptions<Dynamics365Settings> settings,
        SourceQueryCollection queries,
        SqlToXmlService sqlToXmlService,
        IServiceProvider provider,
        ILogger<CommandHandler> logger)
    {
        _settings = settings.Value ?? throw new ArgumentNullException(nameof(settings), "The settings are null");
        _queries = queries;
        _sqlToXmlService = sqlToXmlService;
        _provider = provider;
        _logger = logger;
        _timeout = _settings.ImportTimeout <= 0 ? 60 : _settings.ImportTimeout;
    }

    [Command("export-file", Aliases = new[] { "f" }, Description = "Exports the queries to Xml files")]
    public async Task RunExportToFileAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting exporting to directory : {OutputDirectory}", _queries.OutputDirectory);
        XmlFileOutputFactory output = _provider.GetRequiredService<XmlFileOutputFactory>();
        foreach (string path in Directory.GetFiles(_queries.OutputDirectory))
        {
            string fileName = Path.GetFileName(path);
            _logger.LogInformation("Deleting old file : {FileName}", fileName);
            File.Delete(path);
        }
        try
        {
            _ = await RunQueriesWithDependenciesAsync(output, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Export failed");
            throw;
        }
        _logger.LogInformation("Exported succesfully to directory : {OutputDirectory}", _queries.OutputDirectory);
    }

    [Command("export-package", Aliases = new[] { "p" }, Description = "Exports the queries to zip packages containing the Xml and manifest files.")]
    public async Task RunExportToPackageAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting exporting to directory : {OutputDirectory}", _queries.OutputDirectory);
        XmlPackageFileOutputFactory output = _provider.GetRequiredService<XmlPackageFileOutputFactory>();
        foreach (string path in Directory.GetFiles(_queries.OutputDirectory))
        {
            string fileName = Path.GetFileName(path);
            _logger.LogInformation("Deleting old file : {FileName}", fileName);
            File.Delete(path);
        }
        try
        {
            _ = await RunQueriesWithDependenciesAsync(output, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Export failed");
            throw;
        }
        _logger.LogInformation("Exported succesfully to directory : {OutputDirectory}", _queries.OutputDirectory);
    }

    [Command("import-d365", Aliases = new[] { "i" }, Description = "Import the exported queries directly into Dynamics 365. The Dynamics 365 blobs are created using the dependency order.")]
    public async Task RunImportDynamicsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting importing in Dynamics 365");
        try
        {
            XmlD365FnoOutputFactory output = _provider.GetRequiredService<XmlD365FnoOutputFactory>();

            _ = await RunQueriesWithDependenciesAsync(output, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Export failed");
            throw;
        }
        _logger.LogInformation("Import to Dynamics 365 terminated");
    }

    private async Task<bool> CheckTasksStatus(IEnumerable<IXmlOutputPart> running, CancellationToken cancellationToken)
    {
        IEnumerable<IXmlOutputPart> tasks = running.Reverse();
        bool? failed = null;
        _ = DateTime.UtcNow;
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
                        failed = false;
                        break;

                    case ExecutionStatus.Failed:
                        _logger.LogError("Task {PartName} Failed", t.PartName);
                        failed = true;
                        break;

                    case ExecutionStatus.PartiallySucceeded:
                        _logger.LogError("Task {PartName} partially succeeded", t.PartName);
                        failed = true;
                        break;

                    case ExecutionStatus.Unknown:
                        _logger.LogWarning("Task {PartName} is in an unkown state", t.PartName);
                        remaining.Add(t);
                        break;

                    case ExecutionStatus.Canceled:
                        _logger.LogError("Task {PartName} was cancelled", t.PartName);
                        failed = true;
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
                if ((DateTime.UtcNow - t.StartedTime).TotalMinutes > _timeout && failed != null)
                {
                    _logger.LogError(
                        "Task {PartName} timeout reached. Status={ExecutionStatus}",
                        t.PartName,
                        executionStatus);
                    failed = true;
                    break;
                }
            }
            if (remaining.Count > 0)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    failed = true;
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

        return failed == true;
    }

    private async Task<IEnumerable<IXmlOutputPart>> RunQueriesWithDependenciesAsync(IXmlOutputFactory factory, CancellationToken cancellationToken)
    {
        ConcurrentBag<IXmlOutputPart> parts = new();
        foreach (List<SourceQueryItem> tasks in _queries.SortedQueries)
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
            bool failed = await CheckTasksStatus(running.ToArray(), cancellationToken);
            if (failed)
            {
                _logger.LogError("Stopping export process due to tasks in error state");
            }
        }
        return parts;
    }
}