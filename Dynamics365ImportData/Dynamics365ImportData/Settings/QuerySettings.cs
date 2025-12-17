namespace Dynamics365ImportData.Settings;

public class QuerySettings
{
    public List<string>? Dependencies { get; set; }
    public string? EntityName { get; set; }
    public string? DefinitionGroupId { get; set; }
    public string? ManifestFileName { get; set; }
    public string? PackageHeaderFileName { get; set; }
    public string? QueryFileName { get; set; }
    public int RecordsPerFile { get; set; }
    public string? SourceConnectionString { get; set; }
}