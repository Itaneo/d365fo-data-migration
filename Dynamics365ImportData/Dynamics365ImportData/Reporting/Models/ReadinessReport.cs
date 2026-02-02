namespace Dynamics365ImportData.Reporting.Models;

public class ReadinessReport
{
    public DateTimeOffset GeneratedAt { get; set; }
    public int CyclesAnalyzed { get; set; }
    public int CyclesRequested { get; set; }
    public bool FewerCyclesThanRequested => CyclesAnalyzed < CyclesRequested;
    public List<CycleTrendPoint> CycleTrends { get; set; } = new();
    public List<EntityReadiness> EntityDetails { get; set; } = new();
    public int TotalEntities { get; set; }
    public int EntitiesAtSuccess { get; set; }
    public int EntitiesAtWarning { get; set; }
    public int EntitiesAtFailure { get; set; }
}
