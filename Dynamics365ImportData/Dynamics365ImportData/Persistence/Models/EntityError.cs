namespace Dynamics365ImportData.Persistence.Models;

public class EntityError
{
    public string Message { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public ErrorCategory Category { get; set; }
}
