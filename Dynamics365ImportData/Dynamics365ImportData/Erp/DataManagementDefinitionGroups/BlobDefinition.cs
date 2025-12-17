namespace Dynamics365ImportData.Erp.DataManagementDefinitionGroups;

using System.Runtime.Serialization;

[DataContract]
public class BlobDefinition
{
    [DataMember(IsRequired = true, Order = 1)]
    public string? BlobId { get; set; }

    [DataMember(IsRequired = true, Order = 2)]
    public string? BlobUrl { get; set; }
}