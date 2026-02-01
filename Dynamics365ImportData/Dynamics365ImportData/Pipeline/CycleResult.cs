namespace Dynamics365ImportData.Pipeline;

using Dynamics365ImportData.Persistence.Models;

public class CycleResult
{
    // Existing properties (backward compatible)
    public string Command { get; set; } = string.Empty;
    public int TotalEntities { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }

    // New properties for persistence
    public string CycleId { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string[] EntitiesRequested { get; set; } = [];
    public List<EntityResult> Results { get; set; } = new();
    public CycleSummary? Summary { get; set; }
    public long TotalDurationMs { get; set; }
}
