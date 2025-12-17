namespace Dynamics365ImportData.Erp.DataManagementDefinitionGroups;

using System.Runtime.Serialization;

[DataContract]
public class ExecutionIdRequest
{
    public ExecutionIdRequest(string executionId)
    {
        ExecutionId = executionId;
    }

    [DataMember(IsRequired = true, Name = "executionId")]
    public string ExecutionId { get; set; }
}