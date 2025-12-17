namespace Dynamics365ImportData.Erp.DataManagementDefinitionGroups;

using System.Runtime.Serialization;

[DataContract]
public class UniqueFileNameRequest
{
    public UniqueFileNameRequest(string uniqueFileName)
    {
        UniqueFileName = uniqueFileName;
    }

    [DataMember(IsRequired = true, Name = "uniqueFileName")]
    public string UniqueFileName { get; set; }
}