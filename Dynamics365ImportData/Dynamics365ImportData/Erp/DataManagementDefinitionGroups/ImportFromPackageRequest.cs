namespace Dynamics365ImportData.Erp.DataManagementDefinitionGroups;

using System.Runtime.Serialization;
using System.Text.Json.Serialization;

[DataContract]
public class ImportFromPackageRequest
{
    [DataMember(IsRequired = true, Order = 2, Name = "definitionGroupId")]
    [JsonPropertyOrder(2)]
    public string? DefinitionGroupId { get; set; }

    [DataMember(IsRequired = true, Order = 4, Name = "execute")]
    [JsonPropertyOrder(4)]
    public bool Execute { get; set; }

    [DataMember(IsRequired = true, Order = 3, Name = "executionId")]
    [JsonPropertyOrder(3)]
    public string? ExecutionId { get; set; }

    [DataMember(IsRequired = true, Order = 6, Name = "legalEntityId")]
    [JsonPropertyOrder(6)]
    public string? LegalEntityId { get; set; }

    [DataMember(IsRequired = true, Order = 5, Name = "overwrite")]
    [JsonPropertyOrder(5)]
    public bool Overwrite { get; set; }

    [DataMember(IsRequired = true, Order = 1, Name = "packageUrl")]
    [JsonPropertyOrder(1)]
    public Uri? PackageUrl { get; set; }
}