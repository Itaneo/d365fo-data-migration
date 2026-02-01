namespace Dynamics365ImportData.Tests.TestHelpers;

using System.Text.RegularExpressions;

public class CredentialPatternScanner
{
    private static readonly List<(string Name, Regex Pattern)> CredentialPatterns =
    [
        ("Bearer Token", new Regex(@"Bearer\s+eyJ[A-Za-z0-9\-_]+", RegexOptions.Compiled)),
        ("SAS Token (sig)", new Regex(@"sig=[A-Za-z0-9%+/=]{20,}", RegexOptions.Compiled)),
        ("SAS Token (sv)", new Regex(@"[?&]sv=\d{4}-\d{2}-\d{2}", RegexOptions.Compiled)),
        ("SAS Token (se)", new Regex(@"[?&]se=\d{4}-\d{2}-\d{2}", RegexOptions.Compiled)),
        ("Connection String (Server)", new Regex(@"Server\s*=\s*[^;]+;\s*Database\s*=\s*[^;]+", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
        ("Connection String (Data Source)", new Regex(@"Data\s+Source\s*=\s*[^;]+", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
        ("Client Secret (contextual)", new Regex(@"(?:secret|password|key|token|credential)\s*[=:]\s*[""']?[A-Za-z0-9+/\-_=]{32,}", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
        ("Azure AD Tenant/Client ID in Auth", new Regex(@"(?:client_id|tenant_id|client_secret)\s*[=:]\s*[A-Za-z0-9\-]{20,}", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
    ];

    public List<CredentialMatch> ScanForCredentials(string content)
    {
        var matches = new List<CredentialMatch>();

        foreach (var (name, pattern) in CredentialPatterns)
        {
            foreach (Match match in pattern.Matches(content))
            {
                matches.Add(new CredentialMatch(name, match.Value, match.Index));
            }
        }

        return matches;
    }

    public static List<(string PatternName, Regex Pattern)> GetPatterns() => CredentialPatterns;
}

public record CredentialMatch(string PatternName, string MatchedValue, int Position);
