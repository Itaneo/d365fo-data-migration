namespace Dynamics365ImportData.Sanitization;

using System.Text.RegularExpressions;

public class RegexResultSanitizer : IResultSanitizer
{
    private const string Redacted = "[REDACTED]";

    private static readonly Regex[] Patterns =
    [
        // Connection strings: Server=...;Database=...
        new(@"Server\s*=\s*[^;]+;.*?(?:Database|Initial Catalog)\s*=\s*[^;]+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase),

        // Connection strings: Data Source=...
        new(@"Data Source\s*=\s*[^;]+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase),

        // Password values in connection strings
        new(@"(?:Password|Pwd)\s*=\s*[^;\s]+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase),

        // Bearer tokens
        new(@"Bearer\s+eyJ[A-Za-z0-9_-]+\.eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase),

        // SAS token parameters
        new(@"(?:sig|sv|se|st|sp|spr|srt|ss)\s*=\s*[^&\s]+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase),

        // Client secrets
        new(@"client_secret\s*=\s*[^\s&]+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase),

        // Azure AD tenant/client IDs in auth contexts
        new(@"(?:client_id|tenant_id|tenant)\s*=\s*[0-9a-fA-F-]{36}",
            RegexOptions.Compiled | RegexOptions.IgnoreCase),
    ];

    public string Sanitize(string rawErrorMessage)
    {
        if (string.IsNullOrEmpty(rawErrorMessage))
            return rawErrorMessage;

        var result = rawErrorMessage;
        foreach (var pattern in Patterns)
        {
            result = pattern.Replace(result, Redacted);
        }

        return result;
    }
}
