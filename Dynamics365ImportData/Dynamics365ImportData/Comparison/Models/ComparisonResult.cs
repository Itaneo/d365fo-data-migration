namespace Dynamics365ImportData.Comparison.Models;

public class ComparisonResult
{
    public string CurrentCycleId { get; set; } = string.Empty;
    public string? PreviousCycleId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public bool IsFirstCycle { get; set; }
    public List<EntityComparisonResult> EntityComparisons { get; set; } = new();
    public int TotalNewErrors { get; set; }
    public int TotalCarryOverErrors { get; set; }
    public int TotalResolvedErrors { get; set; }
}
