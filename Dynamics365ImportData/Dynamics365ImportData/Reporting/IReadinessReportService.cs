namespace Dynamics365ImportData.Reporting;

public interface IReadinessReportService
{
    Task<string?> GenerateAsync(
        int? cycleCount = null,
        string? outputPath = null,
        CancellationToken cancellationToken = default);
}
