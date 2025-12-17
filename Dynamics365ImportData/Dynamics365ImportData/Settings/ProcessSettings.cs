namespace Dynamics365ImportData.Settings;

public class ProcessSettings
{
    public string? DefinitionDirectory { get; set; }
    public int MaxDegreeOfParallelism { get; set; }
    public List<QuerySettings>? Queries { get; set; }
}