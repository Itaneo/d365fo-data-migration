namespace Dynamics365ImportData.Settings;

public class PersistenceSettings
{
    public string ResultsDirectory { get; set; } = string.Empty;
    public int MaxCyclesToRetain { get; set; } = 50;
}
