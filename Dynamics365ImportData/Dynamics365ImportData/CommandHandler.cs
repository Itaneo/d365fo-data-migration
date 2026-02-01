namespace Dynamics365ImportData;

using Cocona;

using Dynamics365ImportData.DependencySorting;
using Dynamics365ImportData.Pipeline;

using Microsoft.Extensions.Logging;

internal class CommandHandler
{
    private readonly ILogger<CommandHandler> _logger;
    private readonly IMigrationPipelineService _pipelineService;
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
    public async Task<int> RunExportToFileAsync(
        [Option("entities")] string? entities = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting exporting to directory : {OutputDirectory}", _queries.OutputDirectory);
        ClearOutputDirectory();
        try
        {
            string[]? entityFilter = ParseEntityFilter(entities);
            var result = await _pipelineService.ExecuteAsync(PipelineMode.File, entityFilter, cancellationToken);
            _logger.LogInformation("Exported successfully to directory: {OutputDirectory}", _queries.OutputDirectory);
            return result.Failed > 0 ? 1 : 0;
        }
        catch (EntityValidationException ex)
        {
            _logger.LogError(ex, "Entity validation failed");
            return 2;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Export to file was canceled");
            return 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export failed");
            return 1;
        }
    }

    [Command("export-package", Aliases = new[] { "p" }, Description = "Exports the queries to zip packages containing the Xml and manifest files.")]
    public async Task<int> RunExportToPackageAsync(
        [Option("entities")] string? entities = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting exporting to directory : {OutputDirectory}", _queries.OutputDirectory);
        ClearOutputDirectory();
        try
        {
            string[]? entityFilter = ParseEntityFilter(entities);
            var result = await _pipelineService.ExecuteAsync(PipelineMode.Package, entityFilter, cancellationToken);
            _logger.LogInformation("Exported successfully to directory: {OutputDirectory}", _queries.OutputDirectory);
            return result.Failed > 0 ? 1 : 0;
        }
        catch (EntityValidationException ex)
        {
            _logger.LogError(ex, "Entity validation failed");
            return 2;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Export to package was canceled");
            return 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export failed");
            return 1;
        }
    }

    [Command("import-d365", Aliases = new[] { "i" }, Description = "Import the exported queries directly into Dynamics 365. The Dynamics 365 blobs are created using the dependency order.")]
    public async Task<int> RunImportDynamicsAsync(
        [Option("entities")] string? entities = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting importing in Dynamics 365");
        try
        {
            string[]? entityFilter = ParseEntityFilter(entities);
            var result = await _pipelineService.ExecuteAsync(PipelineMode.D365, entityFilter, cancellationToken);
            _logger.LogInformation("Import to Dynamics 365 completed");
            return result.Failed > 0 ? 1 : 0;
        }
        catch (EntityValidationException ex)
        {
            _logger.LogError(ex, "Entity validation failed");
            return 2;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Import to Dynamics 365 was canceled");
            return 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import failed");
            return 1;
        }
    }

    private static string[]? ParseEntityFilter(string? entities)
    {
        if (string.IsNullOrWhiteSpace(entities))
            return null;

        var parsed = entities.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parsed.Length > 0 ? parsed : null;
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
