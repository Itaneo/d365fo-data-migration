namespace Dynamics365ImportData.Erp.DataManagementDefinitionGroups;

public enum ExecutionStatus
{
    Unknown,
    NotRun,
    Executing,
    Succeeded,
    PartiallySucceeded,
    Failed,
    Canceled
}