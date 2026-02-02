namespace Dynamics365ImportData.Reporting;

using System.Text;

using Dynamics365ImportData.Persistence;
using Dynamics365ImportData.Persistence.Models;
using Dynamics365ImportData.Pipeline;
using Dynamics365ImportData.Reporting.Models;
using Dynamics365ImportData.Settings;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class ReadinessReportService : IReadinessReportService
{
    private readonly IMigrationResultRepository _resultRepository;
    private readonly IOptions<ReportSettings> _reportSettings;
    private readonly IOptions<DestinationSettings> _destinationSettings;
    private readonly ILogger<ReadinessReportService> _logger;

    public ReadinessReportService(
        IMigrationResultRepository resultRepository,
        IOptions<ReportSettings> reportSettings,
        IOptions<DestinationSettings> destinationSettings,
        ILogger<ReadinessReportService> logger)
    {
        _resultRepository = resultRepository;
        _reportSettings = reportSettings;
        _destinationSettings = destinationSettings;
        _logger = logger;
    }

    public async Task<string?> GenerateAsync(
        int? cycleCount = null,
        string? outputPath = null,
        CancellationToken cancellationToken = default)
    {
        var requestedCycles = cycleCount ?? _reportSettings.Value.DefaultCycleRange;

        _logger.LogInformation("Generating readiness report for {CycleCount} cycles", requestedCycles);

        var cycles = await _resultRepository.GetLatestCycleResultsAsync(requestedCycles, cancellationToken);

        if (cycles.Count == 0)
        {
            _logger.LogWarning("No cycle results found; cannot generate readiness report");
            return null;
        }

        var report = BuildReport(cycles, requestedCycles);
        var markdown = BuildMarkdown(report);
        var filePath = outputPath ?? GetDefaultOutputPath(report.GeneratedAt);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(filePath, markdown, cancellationToken);
        _logger.LogInformation("Readiness report written to {ReportPath}", filePath);
        return filePath;
    }

    private ReadinessReport BuildReport(IReadOnlyList<CycleResult> cycles, int requestedCycles)
    {
        // Cycles come latest-first from repository; reverse for oldest-first processing
        var orderedCycles = cycles.Reverse().ToList();
        var settings = _reportSettings.Value;

        var report = new ReadinessReport
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            CyclesAnalyzed = cycles.Count,
            CyclesRequested = requestedCycles
        };

        // Build cycle trend points (uses SuccessThreshold for consistency with Summary table)
        foreach (var cycle in orderedCycles)
        {
            var totalErrors = cycle.Results.Sum(r => r.Errors.Count);
            var succeededEntities = cycle.Results.Count(r => r.Errors.Count <= settings.SuccessThreshold);
            var failedEntities = cycle.Results.Count(r => r.Errors.Count > settings.SuccessThreshold);

            report.CycleTrends.Add(new CycleTrendPoint
            {
                CycleId = cycle.CycleId,
                Timestamp = cycle.Timestamp,
                TotalErrors = totalErrors,
                TotalEntities = cycle.Results.Count,
                SucceededEntities = succeededEntities,
                FailedEntities = failedEntities
            });
        }

        // Build entity details with history from all cycles

        // Collect all entity names across all cycles to handle entities appearing/disappearing
        var allEntityNames = orderedCycles
            .SelectMany(c => c.Results.Select(r => r.EntityName))
            .Distinct()
            .OrderBy(name => name)
            .ToList();

        foreach (var entityName in allEntityNames)
        {
            var errorHistory = orderedCycles
                .Select(c => c.Results.FirstOrDefault(r => r.EntityName == entityName)?.Errors.Count ?? 0)
                .ToList();

            var currentErrors = errorHistory.Last();
            var previousErrors = errorHistory.Count > 1 ? errorHistory[errorHistory.Count - 2] : currentErrors;

            var classification = ClassifyEntity(currentErrors, settings);
            var trend = CalculateTrend(errorHistory);

            report.EntityDetails.Add(new EntityReadiness
            {
                EntityName = entityName,
                StatusClassification = classification,
                Trend = trend,
                CurrentErrors = currentErrors,
                PreviousErrors = previousErrors,
                ErrorHistory = errorHistory
            });
        }

        report.TotalEntities = report.EntityDetails.Count;
        report.EntitiesAtSuccess = report.EntityDetails.Count(e => e.StatusClassification == EntityStatusClassification.Success);
        report.EntitiesAtWarning = report.EntityDetails.Count(e => e.StatusClassification == EntityStatusClassification.Warning);
        report.EntitiesAtFailure = report.EntityDetails.Count(e => e.StatusClassification == EntityStatusClassification.Failure);

        return report;
    }

    private static EntityStatusClassification ClassifyEntity(int errorCount, ReportSettings settings)
    {
        if (errorCount <= settings.SuccessThreshold)
            return EntityStatusClassification.Success;
        if (errorCount <= settings.WarningThreshold)
            return EntityStatusClassification.Warning;
        return EntityStatusClassification.Failure;
    }

    private static TrendDirection CalculateTrend(List<int> errorHistory)
    {
        if (errorHistory.Count <= 1)
            return TrendDirection.Stable;

        var first = errorHistory.First();
        var last = errorHistory.Last();

        if (last < first)
            return TrendDirection.Improving;
        if (last > first)
            return TrendDirection.Degrading;
        return TrendDirection.Stable;
    }

    private static string BuildMarkdown(ReadinessReport report)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("# Migration Readiness Report");
        sb.AppendLine();
        sb.AppendLine($"**Generated:** {report.GeneratedAt:O}");
        sb.AppendLine($"**Cycles Analyzed:** {report.CyclesAnalyzed} of {report.CyclesRequested}");
        sb.AppendLine();

        // Fewer cycles note
        if (report.FewerCyclesThanRequested)
        {
            if (report.CyclesAnalyzed == 1)
            {
                sb.AppendLine("> **Note:** Only 1 cycle available. Trend data requires multiple cycles.");
            }
            else
            {
                sb.AppendLine($"> **Note:** Only {report.CyclesAnalyzed} cycle(s) available of {report.CyclesRequested} requested.");
            }
            sb.AppendLine();
        }

        // Summary table
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine("| Metric | Value |");
        sb.AppendLine("|--------|-------|");
        sb.AppendLine($"| Cycles Analyzed | {report.CyclesAnalyzed} |");
        sb.AppendLine($"| Total Entities | {report.TotalEntities} |");
        sb.AppendLine($"| Entities at Success | {report.EntitiesAtSuccess} |");
        sb.AppendLine($"| Entities at Warning | {report.EntitiesAtWarning} |");
        sb.AppendLine($"| Entities at Failure | {report.EntitiesAtFailure} |");
        sb.AppendLine();

        // Error trends table
        sb.AppendLine("## Error Trends");
        sb.AppendLine();
        sb.AppendLine("| Cycle | Date | Total Errors | Entities | Succeeded | Failed |");
        sb.AppendLine("|-------|------|-------------|----------|-----------|--------|");
        foreach (var trend in report.CycleTrends)
        {
            sb.AppendLine($"| {trend.CycleId} | {trend.Timestamp:yyyy-MM-dd} | {trend.TotalErrors} | {trend.TotalEntities} | {trend.SucceededEntities} | {trend.FailedEntities} |");
        }
        sb.AppendLine();

        // Entity details
        sb.AppendLine("## Entity Details");
        sb.AppendLine();

        foreach (var entity in report.EntityDetails)
        {
            var statusLabel = FormatClassification(entity.StatusClassification);
            var trendLabel = FormatTrend(entity.Trend);
            sb.AppendLine($"### `{entity.EntityName}` - {statusLabel} ({trendLabel})");
            sb.AppendLine();
            sb.AppendLine("| Cycle | Errors |");
            sb.AppendLine("|-------|--------|");
            for (var i = 0; i < report.CycleTrends.Count; i++)
            {
                var errors = i < entity.ErrorHistory.Count ? entity.ErrorHistory[i] : 0;
                sb.AppendLine($"| {report.CycleTrends[i].CycleId} | {errors} |");
            }
            sb.AppendLine();
        }

        // Footer
        sb.AppendLine("---");
        sb.AppendLine("*Generated by d365fo-data-migration*");

        return sb.ToString();
    }

    private static string FormatClassification(EntityStatusClassification classification) => classification switch
    {
        EntityStatusClassification.Success => "pass",
        EntityStatusClassification.Warning => "warn",
        EntityStatusClassification.Failure => "FAIL",
        _ => classification.ToString().ToLowerInvariant()
    };

    private static string FormatTrend(TrendDirection trend) => trend switch
    {
        TrendDirection.Improving => "improving",
        TrendDirection.Stable => "stable",
        TrendDirection.Degrading => "degrading",
        _ => trend.ToString().ToLowerInvariant()
    };

    private string GetDefaultOutputPath(DateTimeOffset generatedAt)
    {
        var outputDir = !string.IsNullOrEmpty(_reportSettings.Value.OutputDirectory)
            ? _reportSettings.Value.OutputDirectory
            : _destinationSettings.Value.OutputDirectory ?? ".";
        return Path.Combine(outputDir, $"readiness-report-{generatedAt:yyyy-MM-ddTHHmmss}.md");
    }
}
