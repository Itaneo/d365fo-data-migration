namespace Dynamics365ImportData.Comparison;

using Dynamics365ImportData.Comparison.Models;

public interface IErrorComparisonService
{
    Task<ComparisonResult> CompareAsync(
        string? currentCycleId = null,
        string? previousCycleId = null,
        CancellationToken cancellationToken = default);
}
