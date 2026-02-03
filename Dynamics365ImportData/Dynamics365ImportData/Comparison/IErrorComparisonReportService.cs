namespace Dynamics365ImportData.Comparison;

using Dynamics365ImportData.Comparison.Models;

public interface IErrorComparisonReportService
{
    Task<string> GenerateReportAsync(
        ComparisonResult comparison,
        string? outputPath = null,
        CancellationToken cancellationToken = default);
}
