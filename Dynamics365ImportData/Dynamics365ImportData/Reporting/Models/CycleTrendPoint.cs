namespace Dynamics365ImportData.Reporting.Models;

public class CycleTrendPoint
{
    public string CycleId { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public int TotalErrors { get; set; }
    public int TotalEntities { get; set; }
    public int SucceededEntities { get; set; }
    public int FailedEntities { get; set; }
}
