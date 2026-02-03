namespace Dynamics365ImportData.Fingerprinting;

public interface IErrorFingerprinter
{
    string ComputeFingerprint(string entityName, string errorMessage);
}
