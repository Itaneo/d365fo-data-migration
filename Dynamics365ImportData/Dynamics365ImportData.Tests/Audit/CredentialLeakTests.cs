namespace Dynamics365ImportData.Tests.Audit;

using Dynamics365ImportData.Persistence;
using Dynamics365ImportData.Persistence.Models;
using Dynamics365ImportData.Pipeline;
using Dynamics365ImportData.Sanitization;
using Dynamics365ImportData.Settings;
using Dynamics365ImportData.Tests.TestHelpers;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

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
    public async Task PersistedResultFile_ContainsNoCredentialPatterns()
    {
        // Arrange -- create a CycleResult with known credential-bearing error messages
        var tempDir = Path.Combine(Path.GetTempPath(), $"cred-audit-{Guid.NewGuid():N}");
        try
        {
            var sanitizer = new RegexResultSanitizer();
            var persistenceSettings = Options.Create(new PersistenceSettings { ResultsDirectory = tempDir });
            var destinationSettings = Options.Create(new DestinationSettings());
            var logger = NullLogger<JsonFileMigrationResultRepository>.Instance;
            var repo = new JsonFileMigrationResultRepository(persistenceSettings, destinationSettings, sanitizer, logger);

            var result = new CycleResult
            {
                Command = "File",
                TotalEntities = 1,
                Succeeded = 0,
                Failed = 1,
                CycleId = "cycle-2026-02-01T120000",
                Timestamp = new DateTimeOffset(2026, 2, 1, 12, 0, 0, TimeSpan.Zero),
                EntitiesRequested = ["all"],
                Results =
                [
                    new EntityResult
                    {
                        EntityName = "Customers",
                        Status = EntityStatus.Failed,
                        Errors =
                        [
                            new EntityError
                            {
                                Message = "Failed: Server=prod-server.database.windows.net;Database=proddb;User Id=admin;Password=SuperSecret123",
                                Category = ErrorCategory.Technical
                            },
                            new EntityError
                            {
                                Message = "Auth failed with Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.abc123def",
                                Category = ErrorCategory.Technical
                            },
                            new EntityError
                            {
                                Message = "SAS error: sig=mysastoken123&sv=2024-01-01",
                                Category = ErrorCategory.Technical
                            },
                            new EntityError
                            {
                                Message = "client_secret=TopSecretClientValue123",
                                Category = ErrorCategory.Technical
                            }
                        ]
                    }
                ],
                Summary = new CycleSummary { TotalEntities = 1, Failed = 1 }
            };

            // Act -- persist via repository (which sanitizes before writing)
            await repo.SaveCycleResultAsync(result);

            // Assert -- read raw file and scan for credential patterns
            var files = Directory.GetFiles(tempDir, "cycle-*.json");
            files.Length.ShouldBe(1);
            var content = await File.ReadAllTextAsync(files[0]);

            var scanner = new CredentialPatternScanner();
            var matches = scanner.ScanForCredentials(content);

            matches.ShouldBeEmpty(
                $"Found {matches.Count} credential pattern(s) in persisted result file:\n" +
                string.Join("\n", matches.Select(m => $"  [{m.PatternName}] at position {m.Position}: {m.MatchedValue[..Math.Min(50, m.MatchedValue.Length)]}...")));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
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
