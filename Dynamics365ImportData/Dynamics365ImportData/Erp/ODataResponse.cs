namespace Dynamics365ImportData.Erp;

using System.Runtime.Serialization;

[DataContract]
public class ODataResponse
{
    [DataMember(IsRequired = true, Name = "@odata.context")]
    public string? Context { get; set; }

    [DataMember(IsRequired = true, Name = "value")]
    public string? Value { get; set; }
}