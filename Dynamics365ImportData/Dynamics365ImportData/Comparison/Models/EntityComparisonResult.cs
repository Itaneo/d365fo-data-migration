namespace Dynamics365ImportData.Comparison.Models;

using Dynamics365ImportData.Persistence.Models;

public class EntityComparisonResult
{
    public string EntityName { get; set; } = string.Empty;
    public EntityStatus CurrentStatus { get; set; }
    public List<ClassifiedError> NewErrors { get; set; } = new();
    public List<ClassifiedError> CarryOverErrors { get; set; } = new();
    public List<string> ResolvedFingerprints { get; set; } = new();
}
