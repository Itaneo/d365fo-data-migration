namespace Dynamics365ImportData.Sanitization;

public interface IResultSanitizer
{
    string Sanitize(string rawErrorMessage);
}
