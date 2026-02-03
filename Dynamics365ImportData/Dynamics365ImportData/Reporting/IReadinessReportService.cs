namespace Dynamics365ImportData.Reporting;

public interface IReadinessReportService
{
    Task<string?> GenerateAsync(
        int? cycleCount = null,
        string? outputPath = null,
        int? successThreshold = null,
        int? warningThreshold = null,
        CancellationToken cancellationToken = default);
}
