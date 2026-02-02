namespace Dynamics365ImportData.Reporting.Models;

public class EntityReadiness
{
    public string EntityName { get; set; } = string.Empty;
    public EntityStatusClassification StatusClassification { get; set; }
    public TrendDirection Trend { get; set; }
    public int CurrentErrors { get; set; }
    public int PreviousErrors { get; set; }
    public List<int> ErrorHistory { get; set; } = new();
}
