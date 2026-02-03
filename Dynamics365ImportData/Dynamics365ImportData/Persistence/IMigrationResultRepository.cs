namespace Dynamics365ImportData.Persistence;

using Dynamics365ImportData.Pipeline;

public interface IMigrationResultRepository
{
    Task SaveCycleResultAsync(CycleResult result, CancellationToken cancellationToken = default);
    Task<CycleResult?> GetCycleResultAsync(string cycleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CycleResult>> GetLatestCycleResultsAsync(int count, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ListCycleIdsAsync(CancellationToken cancellationToken = default);
}
