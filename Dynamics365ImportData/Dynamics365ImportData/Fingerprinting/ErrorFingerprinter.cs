namespace Dynamics365ImportData.Fingerprinting;

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

public class ErrorFingerprinter : IErrorFingerprinter
{
    private static readonly Regex GuidPattern = new(
        @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        RegexOptions.Compiled);

    private static readonly Regex Iso8601Pattern = new(
        @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?",
        RegexOptions.Compiled);

    private static readonly Regex UsDatetimePattern = new(
        @"\d{1,2}/\d{1,2}/\d{4}\s+\d{1,2}:\d{2}(?::\d{2})?(?:\s*(?:AM|PM))?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex DatetimeSpacePattern = new(
        @"\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}",
        RegexOptions.Compiled);

    private static readonly Regex RecordIdPattern = new(
        @"\b\d{5,}\b",
        RegexOptions.Compiled);

    private static readonly Regex WhitespacePattern = new(
        @"\s+",
        RegexOptions.Compiled);

    public string ComputeFingerprint(string entityName, string errorMessage)
    {
        var normalized = Normalize(errorMessage ?? string.Empty);
        var input = $"{entityName ?? string.Empty}|{normalized}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes, 0, 8).ToLowerInvariant();
    }

    private static string Normalize(string message)
    {
        var result = GuidPattern.Replace(message, string.Empty);
        result = Iso8601Pattern.Replace(result, string.Empty);
        result = UsDatetimePattern.Replace(result, string.Empty);
        result = DatetimeSpacePattern.Replace(result, string.Empty);
        result = RecordIdPattern.Replace(result, string.Empty);
        result = WhitespacePattern.Replace(result, " ");
        return result.Trim().ToLowerInvariant();
    }
}
