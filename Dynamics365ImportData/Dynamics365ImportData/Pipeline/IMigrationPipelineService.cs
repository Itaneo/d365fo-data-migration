namespace Dynamics365ImportData.Pipeline;

public interface IMigrationPipelineService
{
    Task<CycleResult> ExecuteAsync(
        PipelineMode mode,
        string[]? entityFilter,
        CancellationToken cancellationToken);
}
