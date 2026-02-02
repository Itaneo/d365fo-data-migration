namespace Dynamics365ImportData.Settings;

public class ReportSettings
{
    public int DefaultCycleRange { get; set; } = 5;
    public int SuccessThreshold { get; set; } = 0;
    public int WarningThreshold { get; set; } = 5;
    public string OutputDirectory { get; set; } = string.Empty;
}
