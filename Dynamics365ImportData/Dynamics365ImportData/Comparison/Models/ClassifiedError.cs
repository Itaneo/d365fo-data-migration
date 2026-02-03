namespace Dynamics365ImportData.Comparison.Models;

using Dynamics365ImportData.Persistence.Models;

public class ClassifiedError
{
    public string EntityName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public ErrorClassification Classification { get; set; }
    public ErrorCategory Category { get; set; }
}
