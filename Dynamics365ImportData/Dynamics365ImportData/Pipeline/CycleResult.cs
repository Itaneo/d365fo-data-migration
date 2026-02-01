namespace Dynamics365ImportData.Pipeline;

public class CycleResult
{
    public string Command { get; set; } = string.Empty;
    public int TotalEntities { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
}
