namespace Dynamics365ImportData.Persistence.Models;

public class CycleSummary
{
    public int TotalEntities { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public int Warnings { get; set; }
    public int Skipped { get; set; }
    public long TotalDurationMs { get; set; }
}
