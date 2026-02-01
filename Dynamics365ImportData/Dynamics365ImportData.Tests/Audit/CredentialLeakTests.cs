namespace Dynamics365ImportData.Tests.Audit;

using Dynamics365ImportData.Tests.TestHelpers;

using Shouldly;

using System.Text.RegularExpressions;

using Xunit;

public class CredentialLeakTests
{
    [Fact]
    public void SampleResultData_NoCredentialPatterns_ZeroMatches()
    {
        // Arrange
        var sampleDataPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Audit", "TestData", "sample-cycle-result.json");
        var content = File.ReadAllText(sampleDataPath);
        var scanner = new CredentialPatternScanner();

        // Act
        var matches = scanner.ScanForCredentials(content);

        // Assert -- no credential patterns should be found in clean sample data
        matches.ShouldBeEmpty(
            $"Found {matches.Count} credential pattern(s) in sample result data:\n" +
            string.Join("\n", matches.Select(m => $"  [{m.PatternName}] at position {m.Position}: {m.MatchedValue[..Math.Min(50, m.MatchedValue.Length)]}...")));
    }

    [Fact]
    public void KnownCredentialPatterns_DetectedByRegex_AllPatternsMatch()
    {
        // Arrange -- test that the regex patterns themselves work
        var scanner = new CredentialPatternScanner();

        // Known credential strings that MUST be detected
        var knownCredentials = new Dictionary<string, string>
        {
            ["Bearer Token"] = "Authorization: Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0",
            ["SAS Token (sig)"] = "https://storage.blob.core.windows.net/container?sig=abc123def456ghi789jkl012mno345pqr",
            ["SAS Token (sv)"] = "https://storage.blob.core.windows.net/container?sv=2024-02-15&sig=test",
            ["SAS Token (se)"] = "https://storage.blob.core.windows.net/container?se=2026-12-31&sig=test",
            ["Connection String (Server)"] = "Server=myserver.database.windows.net;Database=mydb;User Id=admin;Password=secret123",
            ["Connection String (Data Source)"] = "Data Source=tcp:myserver.database.windows.net,1433;Initial Catalog=mydb",
            ["Client Secret"] = "client_secret=aB3dE5fG7hI9jK1lM3nO5pQ7rS9tU1vW3xY5zA7bC9",
            ["Azure AD Client ID"] = "client_id=12345678-abcd-1234-efgh-123456789012",
        };

        foreach (var (patternName, credentialString) in knownCredentials)
        {
            // Act
            var matches = scanner.ScanForCredentials(credentialString);

            // Assert -- each known credential should produce at least one match
            matches.ShouldNotBeEmpty(
                $"Pattern '{patternName}' failed to detect known credential: {credentialString[..Math.Min(80, credentialString.Length)]}");
        }
    }
}
