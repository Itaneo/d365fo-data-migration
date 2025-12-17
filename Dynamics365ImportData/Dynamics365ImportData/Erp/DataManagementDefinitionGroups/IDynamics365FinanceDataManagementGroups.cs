namespace Dynamics365ImportData.Erp.DataManagementDefinitionGroups;

public interface IDynamics365FinanceDataManagementGroups
{
    Task<BlobDefinition> GetAzureWriteUrl(string uniqueFileName, CancellationToken cancellationToken = default);

    Task<string> ImportFromPackage(ImportFromPackageRequest parameters, CancellationToken cancellationToken = default);
    Task<ExecutionStatus> GetExecutionSummaryStatus(string executionId, CancellationToken cancellationToken = default);
}