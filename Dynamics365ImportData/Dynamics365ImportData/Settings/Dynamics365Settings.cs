namespace Dynamics365ImportData.Settings;

public class Dynamics365Settings
{
    public string? ClientId { get; set; }
    public int ImportTimeout { get; set; } = 60;
    public string? LegalEntityId { get; set; }
    public string? Secret { get; set; }
    public string? Tenant { get; set; }
    public Uri? Url { get; set; }
}