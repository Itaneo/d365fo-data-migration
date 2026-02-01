namespace Dynamics365ImportData;

using Cocona;

using Dynamics365ImportData.DependencySorting;
using Dynamics365ImportData.Pipeline;

using Microsoft.Extensions.Logging;

internal class CommandHandler
{
    private readonly ILogger<CommandHandler> _logger;
    private readonly IMigrationPipelineService _pipelineService;
    // TODO: Story 3.1 - Consider replacing SourceQueryCollection dependency with IOptions<T> for output directory access
    private readonly SourceQueryCollection _queries;

    public CommandHandler(
        IMigrationPipelineService pipelineService,
        SourceQueryCollection queries,
        ILogger<CommandHandler> logger)
    {
        _pipelineService = pipelineService;
        _queries = queries;
        _logger = logger;
    }

    [Command("export-file", Aliases = new[] { "f" }, Description = "Exports the queries to Xml files")]
    public async Task RunExportToFileAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting exporting to directory : {OutputDirectory}", _queries.OutputDirectory);
        ClearOutputDirectory();
        try
        {
            var result = await _pipelineService.ExecuteAsync(PipelineMode.File, entityFilter: null, cancellationToken);
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
        ClearOutputDirectory();
        try
        {
            var result = await _pipelineService.ExecuteAsync(PipelineMode.Package, entityFilter: null, cancellationToken);
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
            var result = await _pipelineService.ExecuteAsync(PipelineMode.D365, entityFilter: null, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Export failed");
            throw;
        }
        _logger.LogInformation("Import to Dynamics 365 terminated");
    }

    private void ClearOutputDirectory()
    {
        foreach (string path in Directory.GetFiles(_queries.OutputDirectory))
        {
            string fileName = Path.GetFileName(path);
            _logger.LogInformation("Deleting old file : {FileName}", fileName);
            File.Delete(path);
        }
    }
}
