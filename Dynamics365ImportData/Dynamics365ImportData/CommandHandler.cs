namespace Dynamics365ImportData;

using Cocona;

using Dynamics365ImportData.Comparison;
using Dynamics365ImportData.Comparison.Models;
using Dynamics365ImportData.DependencySorting;
using Dynamics365ImportData.Persistence;
using Dynamics365ImportData.Pipeline;
using Dynamics365ImportData.Reporting;

using System.Globalization;

using Microsoft.Extensions.Logging;

internal class CommandHandler
{
    private readonly IErrorComparisonService _comparisonService;
    private readonly ILogger<CommandHandler> _logger;
    private readonly IMigrationPipelineService _pipelineService;
    private readonly SourceQueryCollection _queries;
    private readonly IReadinessReportService _readinessReportService;
    private readonly IErrorComparisonReportService _reportService;
    private readonly IMigrationResultRepository _resultRepository;

    public CommandHandler(
        IMigrationPipelineService pipelineService,
        SourceQueryCollection queries,
        IMigrationResultRepository resultRepository,
        IErrorComparisonService comparisonService,
        IErrorComparisonReportService reportService,
        IReadinessReportService readinessReportService,
        ILogger<CommandHandler> logger)
    {
        _pipelineService = pipelineService;
        _queries = queries;
        _resultRepository = resultRepository;
        _comparisonService = comparisonService;
        _reportService = reportService;
        _readinessReportService = readinessReportService;
        _logger = logger;
    }

    [Command("export-file", Aliases = new[] { "f" }, Description = "Exports the queries to Xml files")]
    public async Task<int> RunExportToFileAsync(
        [Option("entities")] string? entities = null,
        [Option("compare")] bool compare = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting exporting to directory : {OutputDirectory}", _queries.OutputDirectory);
        ClearOutputDirectory();
        try
        {
            string[]? entityFilter = ParseEntityFilter(entities);
            var result = await _pipelineService.ExecuteAsync(PipelineMode.File, entityFilter, cancellationToken);
            await PersistResultAsync(result, cancellationToken);
            if (compare)
            {
                await GenerateComparisonReportAsync(cancellationToken);
            }
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
        [Option("compare")] bool compare = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting exporting to directory : {OutputDirectory}", _queries.OutputDirectory);
        ClearOutputDirectory();
        try
        {
            string[]? entityFilter = ParseEntityFilter(entities);
            var result = await _pipelineService.ExecuteAsync(PipelineMode.Package, entityFilter, cancellationToken);
            await PersistResultAsync(result, cancellationToken);
            if (compare)
            {
                await GenerateComparisonReportAsync(cancellationToken);
            }
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
        [Option("compare")] bool compare = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting importing in Dynamics 365");
        try
        {
            string[]? entityFilter = ParseEntityFilter(entities);
            var result = await _pipelineService.ExecuteAsync(PipelineMode.D365, entityFilter, cancellationToken);
            await PersistResultAsync(result, cancellationToken);
            if (compare)
            {
                await GenerateComparisonReportAsync(cancellationToken);
            }
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

    [Command("compare-errors", Aliases = new[] { "ce" }, Description = "Generate error comparison report from recent migration cycles")]
    public async Task<int> RunCompareErrorsAsync(
        [Option("cycle")] string? cycleId = null,
        [Option("output")] string? outputPath = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var comparison = await _comparisonService.CompareAsync(
                currentCycleId: null, previousCycleId: cycleId, cancellationToken: cancellationToken);

            if (comparison.IsFirstCycle && string.IsNullOrEmpty(comparison.CurrentCycleId))
            {
                _logger.LogWarning("No cycle results found");
                return 1;
            }

            var reportPath = await _reportService.GenerateReportAsync(
                comparison, outputPath, cancellationToken);
            _logger.LogInformation("Error comparison report generated: {ReportPath}", reportPath);
            return 0;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Error comparison was canceled");
            return 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate error comparison report");
            return 1;
        }
    }

    [Command("readiness-report", Aliases = new[] { "rr" }, Description = "Generate migration readiness report across multiple cycles")]
    public async Task<int> RunReadinessReportAsync(
        [Option("cycles")] int cycles = 5,
        [Option("threshold")] string? thresholdConfig = null,
        [Option("output")] string? outputPath = null,
        CancellationToken cancellationToken = default)
    {
        if (cycles <= 0)
        {
            _logger.LogError("Invalid cycles value {Cycles}. Must be a positive integer", cycles);
            return 2;
        }

        int? successThreshold = null;
        int? warningThreshold = null;

        if (thresholdConfig is not null)
        {
            try
            {
                (successThreshold, warningThreshold) = ParseThresholdConfig(thresholdConfig);
            }
            catch (FormatException ex)
            {
                _logger.LogError("Invalid threshold configuration: {ErrorMessage}", ex.Message);
                return 2;
            }
        }

        try
        {
            var reportPath = await _readinessReportService.GenerateAsync(
                cycles, outputPath, successThreshold, warningThreshold, cancellationToken);

            if (reportPath is null)
            {
                _logger.LogWarning("No cycle results found; readiness report not generated");
                return 1;
            }

            _logger.LogInformation("Readiness report generated: {ReportPath}", reportPath);
            return 0;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Readiness report generation was canceled");
            return 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate readiness report");
            return 1;
        }
    }

    private static (int? successThreshold, int? warningThreshold) ParseThresholdConfig(string thresholdConfig)
    {
        int? success = null, warning = null;
        foreach (var part in thresholdConfig.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = part.Split(':', 2);
            if (kv.Length != 2)
                throw new FormatException($"Invalid threshold format: '{part}'. Expected 'key:value'.");
            switch (kv[0].Trim().ToLowerInvariant())
            {
                case "success":
                    if (!int.TryParse(kv[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var successVal))
                        throw new FormatException($"Invalid value for 'success' threshold: '{kv[1].Trim()}'. Expected an integer.");
                    success = successVal;
                    break;
                case "warning":
                    if (!int.TryParse(kv[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var warningVal))
                        throw new FormatException($"Invalid value for 'warning' threshold: '{kv[1].Trim()}'. Expected an integer.");
                    warning = warningVal;
                    break;
                default:
                    throw new FormatException($"Unknown threshold key: '{kv[0]}'. Valid keys: success, warning.");
            }
        }
        return (success, warning);
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

    private async Task PersistResultAsync(CycleResult result, CancellationToken cancellationToken)
    {
        try
        {
            await _resultRepository.SaveCycleResultAsync(result, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist migration results for cycle {CycleId}", result.CycleId);
        }
    }

    private async Task GenerateComparisonReportAsync(CancellationToken cancellationToken)
    {
        try
        {
            var comparison = await _comparisonService.CompareAsync(
                cancellationToken: cancellationToken);
            var reportPath = await _reportService.GenerateReportAsync(
                comparison, cancellationToken: cancellationToken);
            _logger.LogInformation("Error comparison report written to {ReportPath}", reportPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate error comparison report");
        }
    }
}
