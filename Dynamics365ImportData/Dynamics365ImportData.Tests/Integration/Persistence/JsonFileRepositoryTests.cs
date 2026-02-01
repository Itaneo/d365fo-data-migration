namespace Dynamics365ImportData.Tests.Integration.Persistence;

using Dynamics365ImportData.Persistence;
using Dynamics365ImportData.Persistence.Models;
using Dynamics365ImportData.Pipeline;
using Dynamics365ImportData.Sanitization;
using Dynamics365ImportData.Settings;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

using Xunit;

public class JsonFileRepositoryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly JsonFileMigrationResultRepository _repository;
    private readonly IResultSanitizer _sanitizer;

    public JsonFileRepositoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"repo-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _sanitizer = new RegexResultSanitizer();
        var persistenceSettings = Options.Create(new PersistenceSettings { ResultsDirectory = _tempDir });
        var destinationSettings = Options.Create(new DestinationSettings());
        var logger = NullLogger<JsonFileMigrationResultRepository>.Instance;

        _repository = new JsonFileMigrationResultRepository(persistenceSettings, destinationSettings, _sanitizer, logger);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task SaveCycleResultAsync_WritesJsonFile_ToResultsDirectory()
    {
        // Arrange
        var result = CreateTestCycleResult();

        // Act
        await _repository.SaveCycleResultAsync(result);

        // Assert
        var files = Directory.GetFiles(_tempDir, "cycle-*.json");
        files.Length.ShouldBe(1);
    }

    [Fact]
    public async Task SaveCycleResultAsync_AtomicWrite_TempFileNotLeftBehind()
    {
        // Arrange
        var result = CreateTestCycleResult();

        // Act
        await _repository.SaveCycleResultAsync(result);

        // Assert -- no temp files left behind
        var tempFiles = Directory.GetFiles(_tempDir, ".tmp-*");
        tempFiles.Length.ShouldBe(0);
    }

    [Fact]
    public async Task SaveCycleResultAsync_SanitizesErrors_BeforeWriting()
    {
        // Arrange
        var result = CreateTestCycleResult();
        var originalMessage = "Failed: Server=myserver.database.windows.net;Database=secretdb;User=admin;Password=p@ss";
        result.Results[0].Errors.Add(new EntityError
        {
            Message = originalMessage,
            Category = ErrorCategory.Technical
        });

        // Act
        await _repository.SaveCycleResultAsync(result);

        // Assert -- read raw file and verify credentials are redacted
        var files = Directory.GetFiles(_tempDir, "cycle-*.json");
        var content = await File.ReadAllTextAsync(files[0]);
        content.ShouldContain("[REDACTED]");
        content.ShouldNotContain("myserver.database.windows.net");
        content.ShouldNotContain("secretdb");
        content.ShouldNotContain("p@ss");

        // Assert -- original in-memory object must NOT be mutated (non-fatal persistence contract)
        result.Results[0].Errors[0].Message.ShouldBe(originalMessage);
    }

    [Fact]
    public async Task GetCycleResultAsync_ExistingCycle_ReturnsDeserializedResult()
    {
        // Arrange
        var result = CreateTestCycleResult();
        await _repository.SaveCycleResultAsync(result);

        // Act
        var loaded = await _repository.GetCycleResultAsync(result.CycleId);

        // Assert
        loaded.ShouldNotBeNull();
        loaded.CycleId.ShouldBe(result.CycleId);
        loaded.Command.ShouldBe(result.Command);
        loaded.Results.Count.ShouldBe(result.Results.Count);
    }

    [Fact]
    public async Task GetCycleResultAsync_NonExistentCycle_ReturnsNull()
    {
        // Arrange & Act
        var loaded = await _repository.GetCycleResultAsync("cycle-nonexistent");

        // Assert
        loaded.ShouldBeNull();
    }

    [Fact]
    public async Task GetLatestCycleResultsAsync_MultipleFiles_ReturnsInDescendingOrder()
    {
        // Arrange
        var result1 = CreateTestCycleResult(new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero));
        var result2 = CreateTestCycleResult(new DateTimeOffset(2026, 1, 1, 11, 0, 0, TimeSpan.Zero));
        var result3 = CreateTestCycleResult(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

        await _repository.SaveCycleResultAsync(result1);
        await _repository.SaveCycleResultAsync(result2);
        await _repository.SaveCycleResultAsync(result3);

        // Act
        var latest = await _repository.GetLatestCycleResultsAsync(2);

        // Assert
        latest.Count.ShouldBe(2);
        latest[0].CycleId.ShouldBe(result3.CycleId);
        latest[1].CycleId.ShouldBe(result2.CycleId);
    }

    [Fact]
    public async Task ListCycleIdsAsync_MultipleFiles_ReturnsAllIds()
    {
        // Arrange
        var result1 = CreateTestCycleResult(new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero));
        var result2 = CreateTestCycleResult(new DateTimeOffset(2026, 1, 1, 11, 0, 0, TimeSpan.Zero));

        await _repository.SaveCycleResultAsync(result1);
        await _repository.SaveCycleResultAsync(result2);

        // Act
        var ids = await _repository.ListCycleIdsAsync();

        // Assert
        ids.Count.ShouldBe(2);
        ids.ShouldContain(result1.CycleId);
        ids.ShouldContain(result2.CycleId);
    }

    [Fact]
    public async Task SaveCycleResultAsync_DirectoryDoesNotExist_CreatesDirectory()
    {
        // Arrange
        var nestedDir = Path.Combine(_tempDir, "nested", "results");
        var persistenceSettings = Options.Create(new PersistenceSettings { ResultsDirectory = nestedDir });
        var destinationSettings = Options.Create(new DestinationSettings());
        var logger = NullLogger<JsonFileMigrationResultRepository>.Instance;
        var repo = new JsonFileMigrationResultRepository(persistenceSettings, destinationSettings, _sanitizer, logger);
        var result = CreateTestCycleResult();

        // Act
        await repo.SaveCycleResultAsync(result);

        // Assert
        Directory.Exists(nestedDir).ShouldBeTrue();
        Directory.GetFiles(nestedDir, "cycle-*.json").Length.ShouldBe(1);
    }

    private static CycleResult CreateTestCycleResult(DateTimeOffset? timestamp = null)
    {
        var ts = timestamp ?? DateTimeOffset.UtcNow;
        return new CycleResult
        {
            Command = "File",
            TotalEntities = 1,
            Succeeded = 1,
            Failed = 0,
            CycleId = $"cycle-{ts:yyyy-MM-ddTHHmmss}",
            Timestamp = ts,
            EntitiesRequested = ["all"],
            Results =
            [
                new EntityResult
                {
                    EntityName = "Customers",
                    DefinitionGroupId = "CustImport",
                    Status = EntityStatus.Success,
                    RecordCount = 0,
                    DurationMs = 500
                }
            ],
            Summary = new CycleSummary
            {
                TotalEntities = 1,
                Succeeded = 1,
                Failed = 0,
                TotalDurationMs = 500
            },
            TotalDurationMs = 500
        };
    }
}
