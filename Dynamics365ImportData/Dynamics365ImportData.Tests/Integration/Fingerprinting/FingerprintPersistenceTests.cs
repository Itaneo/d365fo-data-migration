namespace Dynamics365ImportData.Tests.Integration.Fingerprinting;

using Dynamics365ImportData.Persistence;
using Dynamics365ImportData.Persistence.Models;
using Dynamics365ImportData.Pipeline;
using Dynamics365ImportData.Sanitization;
using Dynamics365ImportData.Settings;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

public class FingerprintPersistenceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly JsonFileMigrationResultRepository _repository;

    public FingerprintPersistenceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"fp-persist-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        var sanitizer = new RegexResultSanitizer();
        var persistenceSettings = Options.Create(new PersistenceSettings { ResultsDirectory = _tempDir });
        var destinationSettings = Options.Create(new DestinationSettings());
        var logger = NullLogger<JsonFileMigrationResultRepository>.Instance;

        _repository = new JsonFileMigrationResultRepository(persistenceSettings, destinationSettings, sanitizer, logger);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task Fingerprint_PersistedInResultJson_DeserializesCorrectly()
    {
        // Arrange
        var fingerprint = "aabb112233445566";
        var result = new CycleResult
        {
            Command = "File",
            TotalEntities = 1,
            Succeeded = 0,
            Failed = 1,
            CycleId = $"cycle-{DateTimeOffset.UtcNow:yyyy-MM-ddTHHmmss}",
            Timestamp = DateTimeOffset.UtcNow,
            EntitiesRequested = ["all"],
            Results =
            [
                new EntityResult
                {
                    EntityName = "Customers",
                    DefinitionGroupId = "CustImport",
                    Status = EntityStatus.Failed,
                    Errors =
                    [
                        new EntityError
                        {
                            Message = "Record not found in table",
                            Fingerprint = fingerprint,
                            Category = ErrorCategory.Technical
                        }
                    ]
                }
            ],
            Summary = new CycleSummary { TotalEntities = 1, Failed = 1 },
            TotalDurationMs = 100
        };

        // Act
        await _repository.SaveCycleResultAsync(result);
        var loaded = await _repository.GetCycleResultAsync(result.CycleId);

        // Assert
        loaded.ShouldNotBeNull();
        loaded.Results[0].Errors[0].Fingerprint.ShouldBe(fingerprint);
    }

    [Fact]
    public async Task Fingerprint_InJsonOutput_UsesCamelCase()
    {
        // Arrange
        var result = new CycleResult
        {
            Command = "File",
            TotalEntities = 1,
            Succeeded = 0,
            Failed = 1,
            CycleId = $"cycle-{DateTimeOffset.UtcNow:yyyy-MM-ddTHHmmss}",
            Timestamp = DateTimeOffset.UtcNow,
            EntitiesRequested = ["all"],
            Results =
            [
                new EntityResult
                {
                    EntityName = "Customers",
                    DefinitionGroupId = "CustImport",
                    Status = EntityStatus.Failed,
                    Errors =
                    [
                        new EntityError
                        {
                            Message = "Test error",
                            Fingerprint = "1122334455667788",
                            Category = ErrorCategory.Technical
                        }
                    ]
                }
            ],
            Summary = new CycleSummary { TotalEntities = 1, Failed = 1 },
            TotalDurationMs = 100
        };

        // Act
        await _repository.SaveCycleResultAsync(result);

        // Assert -- read raw JSON to verify camelCase key
        var files = Directory.GetFiles(_tempDir, "cycle-*.json");
        var content = await File.ReadAllTextAsync(files[0]);
        content.ShouldContain("\"fingerprint\":");
        content.ShouldNotContain("\"Fingerprint\":", Case.Sensitive);
    }
}
