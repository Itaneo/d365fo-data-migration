namespace Dynamics365ImportData.Tests.Unit.Reporting;

using System.Diagnostics;

using Dynamics365ImportData.Persistence;
using Dynamics365ImportData.Persistence.Models;
using Dynamics365ImportData.Pipeline;
using Dynamics365ImportData.Reporting;
using Dynamics365ImportData.Settings;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

using Xunit;

public class ReadinessReportServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly IMigrationResultRepository _repository;
    private readonly IOptions<ReportSettings> _reportSettings;
    private readonly IOptions<DestinationSettings> _destinationSettings;
    private readonly ReadinessReportService _service;

    public ReadinessReportServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ReadinessTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _repository = Substitute.For<IMigrationResultRepository>();

        _reportSettings = Options.Create(new ReportSettings
        {
            DefaultCycleRange = 5,
            SuccessThreshold = 0,
            WarningThreshold = 5,
            OutputDirectory = _tempDir
        });

        _destinationSettings = Options.Create(new DestinationSettings
        {
            OutputDirectory = _tempDir
        });

        _service = new ReadinessReportService(
            _repository,
            _reportSettings,
            _destinationSettings,
            NullLogger<ReadinessReportService>.Instance);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); }
        catch { /* cleanup best-effort */ }
    }

    #region Helper Methods

    private static CycleResult CreateCycle(
        string cycleId,
        DateTimeOffset timestamp,
        params EntityResult[] results)
    {
        return new CycleResult
        {
            CycleId = cycleId,
            Timestamp = timestamp,
            Results = results.ToList(),
            TotalEntities = results.Length,
            Succeeded = results.Count(r => r.Errors.Count == 0),
            Failed = results.Count(r => r.Errors.Count > 0)
        };
    }

    private static EntityResult CreateEntityResult(string entityName, int errorCount)
    {
        return new EntityResult
        {
            EntityName = entityName,
            Status = errorCount == 0 ? EntityStatus.Success : EntityStatus.Failed,
            Errors = Enumerable.Range(0, errorCount)
                .Select(i => new EntityError
                {
                    Message = $"Error {i} for {entityName}",
                    Fingerprint = $"{entityName.GetHashCode():X8}{i:D8}"
                }).ToList()
        };
    }

    private void SetupRepository(params CycleResult[] cycles)
    {
        // Repository returns latest-first
        var cycleList = cycles.OrderByDescending(c => c.Timestamp).ToList();
        _repository.GetLatestCycleResultsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CycleResult>>(cycleList));
    }

    private List<CycleResult> CreateMultipleCycles()
    {
        return new List<CycleResult>
        {
            CreateCycle("cycle-2026-01-28T100000",
                new DateTimeOffset(2026, 1, 28, 10, 0, 0, TimeSpan.Zero),
                CreateEntityResult("CustCustomerV3Entity", 5),
                CreateEntityResult("smmContactPersonV2Entity", 10),
                CreateEntityResult("CustomerBankAccountEntity", 2)),
            CreateCycle("cycle-2026-01-29T100000",
                new DateTimeOffset(2026, 1, 29, 10, 0, 0, TimeSpan.Zero),
                CreateEntityResult("CustCustomerV3Entity", 8),
                CreateEntityResult("smmContactPersonV2Entity", 3),
                CreateEntityResult("CustomerBankAccountEntity", 2)),
            CreateCycle("cycle-2026-01-30T100000",
                new DateTimeOffset(2026, 1, 30, 10, 0, 0, TimeSpan.Zero),
                CreateEntityResult("CustCustomerV3Entity", 6),
                CreateEntityResult("smmContactPersonV2Entity", 0),
                CreateEntityResult("CustomerBankAccountEntity", 2))
        };
    }

    #endregion

    [Fact]
    public async Task GenerateAsync_MultipleCycles_AggregatesAllCycles()
    {
        // Arrange
        var cycles = CreateMultipleCycles();
        SetupRepository(cycles.ToArray());

        // Act
        var filePath = await _service.GenerateAsync();

        // Assert
        filePath.ShouldNotBeNull();
        var content = await File.ReadAllTextAsync(filePath);
        content.ShouldContain("| Cycles Analyzed | 3 |");
        content.ShouldContain("| Total Entities | 3 |");
    }

    [Fact]
    public async Task GenerateAsync_ErrorTrends_ShowsTotalErrorsPerCycle()
    {
        // Arrange
        var cycles = CreateMultipleCycles();
        SetupRepository(cycles.ToArray());

        // Act
        var filePath = await _service.GenerateAsync();

        // Assert
        filePath.ShouldNotBeNull();
        var content = await File.ReadAllTextAsync(filePath);
        content.ShouldContain("## Error Trends");
        // cycle-2026-01-28: 5+10+2=17 total errors, 0 succeeded, 3 failed
        content.ShouldContain("| cycle-2026-01-28T100000 | 2026-01-28 | 17 | 3 | 0 | 3 |");
        // cycle-2026-01-29: 8+3+2=13 total errors, 0 succeeded, 3 failed
        content.ShouldContain("| cycle-2026-01-29T100000 | 2026-01-29 | 13 | 3 | 0 | 3 |");
        // cycle-2026-01-30: 6+0+2=8 total errors, 1 succeeded (smm=0), 2 failed
        content.ShouldContain("| cycle-2026-01-30T100000 | 2026-01-30 | 8 | 3 | 1 | 2 |");
    }

    [Fact]
    public async Task GenerateAsync_EntityStatus_ClassifiesCorrectly()
    {
        // Arrange -- single cycle: 0 errors=success, 3 errors=warning (<=5), 10 errors=failure (>5)
        var cycle = CreateCycle("cycle-1",
            DateTimeOffset.UtcNow,
            CreateEntityResult("SuccessEntity", 0),
            CreateEntityResult("WarningEntity", 3),
            CreateEntityResult("FailureEntity", 10));
        SetupRepository(cycle);

        // Act
        var filePath = await _service.GenerateAsync();

        // Assert
        filePath.ShouldNotBeNull();
        var content = await File.ReadAllTextAsync(filePath);
        content.ShouldContain("### `SuccessEntity` - pass");
        content.ShouldContain("### `WarningEntity` - warn");
        content.ShouldContain("### `FailureEntity` - FAIL");
    }

    [Fact]
    public async Task GenerateAsync_ConvergenceDirection_DetectsImproving()
    {
        // Arrange -- errors decreasing: 10 -> 5
        var cycles = new[]
        {
            CreateCycle("cycle-1", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                CreateEntityResult("Entity1", 10)),
            CreateCycle("cycle-2", new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero),
                CreateEntityResult("Entity1", 5))
        };
        SetupRepository(cycles);

        // Act
        var filePath = await _service.GenerateAsync();

        // Assert
        filePath.ShouldNotBeNull();
        var content = await File.ReadAllTextAsync(filePath);
        content.ShouldContain("### `Entity1` - warn (improving)");
    }

    [Fact]
    public async Task GenerateAsync_ConvergenceDirection_DetectsStable()
    {
        // Arrange -- errors unchanged: 3 -> 3
        var cycles = new[]
        {
            CreateCycle("cycle-1", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                CreateEntityResult("Entity1", 3)),
            CreateCycle("cycle-2", new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero),
                CreateEntityResult("Entity1", 3))
        };
        SetupRepository(cycles);

        // Act
        var filePath = await _service.GenerateAsync();

        // Assert
        filePath.ShouldNotBeNull();
        var content = await File.ReadAllTextAsync(filePath);
        content.ShouldContain("### `Entity1` - warn (stable)");
    }

    [Fact]
    public async Task GenerateAsync_ConvergenceDirection_DetectsDegrading()
    {
        // Arrange -- errors increasing: 2 -> 8
        var cycles = new[]
        {
            CreateCycle("cycle-1", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                CreateEntityResult("Entity1", 2)),
            CreateCycle("cycle-2", new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero),
                CreateEntityResult("Entity1", 8))
        };
        SetupRepository(cycles);

        // Act
        var filePath = await _service.GenerateAsync();

        // Assert
        filePath.ShouldNotBeNull();
        var content = await File.ReadAllTextAsync(filePath);
        content.ShouldContain("### `Entity1` - FAIL (degrading)");
    }

    [Fact]
    public async Task GenerateAsync_MarkdownFormat_FollowsTemplate()
    {
        // Arrange
        var cycles = CreateMultipleCycles();
        SetupRepository(cycles.ToArray());

        // Act
        var filePath = await _service.GenerateAsync();

        // Assert
        filePath.ShouldNotBeNull();
        var content = await File.ReadAllTextAsync(filePath);
        content.ShouldContain("# Migration Readiness Report");
        content.ShouldContain("**Generated:**");
        content.ShouldContain("**Cycles Analyzed:**");
        content.ShouldContain("## Summary");
        content.ShouldContain("## Error Trends");
        content.ShouldContain("## Entity Details");
        content.ShouldContain("---");
        content.ShouldContain("*Generated by d365fo-data-migration*");
    }

    [Fact]
    public async Task GenerateAsync_EntityNames_UseCodeFormatting()
    {
        // Arrange
        var cycle = CreateCycle("cycle-1", DateTimeOffset.UtcNow,
            CreateEntityResult("CustCustomerV3Entity", 0));
        SetupRepository(cycle);

        // Act
        var filePath = await _service.GenerateAsync();

        // Assert
        filePath.ShouldNotBeNull();
        var content = await File.ReadAllTextAsync(filePath);
        content.ShouldContain("### `CustCustomerV3Entity`");
    }

    [Fact]
    public async Task GenerateAsync_StatusIndicators_UseCorrectConvention()
    {
        // Arrange
        var cycle = CreateCycle("cycle-1", DateTimeOffset.UtcNow,
            CreateEntityResult("PassEntity", 0),
            CreateEntityResult("WarnEntity", 3),
            CreateEntityResult("FailEntity", 10));
        SetupRepository(cycle);

        // Act
        var filePath = await _service.GenerateAsync();

        // Assert
        filePath.ShouldNotBeNull();
        var content = await File.ReadAllTextAsync(filePath);
        content.ShouldContain("- pass");
        content.ShouldContain("- warn");
        content.ShouldContain("- FAIL");
    }

    [Fact]
    public async Task GenerateAsync_DefaultCycleRange_UsesReportSettings()
    {
        // Arrange
        var cycles = CreateMultipleCycles();
        SetupRepository(cycles.ToArray());

        // Act
        await _service.GenerateAsync();

        // Assert -- verify repository was called with the default range from settings (5)
        await _repository.Received(1).GetLatestCycleResultsAsync(5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_CustomCycleCount_OverridesDefault()
    {
        // Arrange
        var cycles = CreateMultipleCycles();
        SetupRepository(cycles.ToArray());

        // Act
        await _service.GenerateAsync(cycleCount: 3);

        // Assert -- verify repository was called with the custom count (3)
        await _repository.Received(1).GetLatestCycleResultsAsync(3, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_FewerCyclesThanRequested_IncludesNote()
    {
        // Arrange -- request 5 but only 3 available
        var cycles = CreateMultipleCycles();
        SetupRepository(cycles.ToArray());

        // Act
        var filePath = await _service.GenerateAsync(cycleCount: 5);

        // Assert
        filePath.ShouldNotBeNull();
        var content = await File.ReadAllTextAsync(filePath);
        content.ShouldContain("> **Note:** Only 3 cycle(s) available of 5 requested.");
    }

    [Fact]
    public async Task GenerateAsync_SingleCycle_ShowsCurrentStateWithNote()
    {
        // Arrange
        var cycle = CreateCycle("cycle-2026-02-01T143000",
            new DateTimeOffset(2026, 2, 1, 14, 30, 0, TimeSpan.Zero),
            CreateEntityResult("CustCustomerV3Entity", 6),
            CreateEntityResult("smmContactPersonV2Entity", 0));
        SetupRepository(cycle);

        // Act
        var filePath = await _service.GenerateAsync(cycleCount: 5);

        // Assert
        filePath.ShouldNotBeNull();
        var content = await File.ReadAllTextAsync(filePath);
        content.ShouldContain("> **Note:** Only 1 cycle available. Trend data requires multiple cycles.");
        content.ShouldContain("**Cycles Analyzed:** 1 of 5");
        // Single cycle means all trends should be stable
        content.ShouldContain("(stable)");
        content.ShouldNotContain("(improving)");
        content.ShouldNotContain("(degrading)");
    }

    [Fact]
    public async Task GenerateAsync_NoCycles_ReturnsNull()
    {
        // Arrange
        _repository.GetLatestCycleResultsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CycleResult>>(new List<CycleResult>()));

        // Act
        var result = await _service.GenerateAsync();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GenerateAsync_CustomOutputPath_WritesToSpecifiedPath()
    {
        // Arrange
        var cycle = CreateCycle("cycle-1", DateTimeOffset.UtcNow,
            CreateEntityResult("Entity1", 0));
        SetupRepository(cycle);
        var customPath = Path.Combine(_tempDir, "custom", "my-readiness-report.md");

        // Act
        var filePath = await _service.GenerateAsync(outputPath: customPath);

        // Assert
        filePath.ShouldBe(customPath);
        File.Exists(customPath).ShouldBeTrue();
    }

    [Fact]
    public async Task GenerateAsync_DefaultOutputPath_UsesReportSettingsOrDestination()
    {
        // Arrange
        var cycle = CreateCycle("cycle-1", DateTimeOffset.UtcNow,
            CreateEntityResult("Entity1", 0));
        SetupRepository(cycle);

        // Act
        var filePath = await _service.GenerateAsync();

        // Assert
        filePath.ShouldNotBeNull();
        filePath.ShouldStartWith(_tempDir);
        filePath.ShouldContain("readiness-report-");
        filePath.ShouldEndWith(".md");
    }

    [Fact]
    public async Task GenerateAsync_ContainsFooter()
    {
        // Arrange
        var cycle = CreateCycle("cycle-1", DateTimeOffset.UtcNow,
            CreateEntityResult("Entity1", 0));
        SetupRepository(cycle);

        // Act
        var filePath = await _service.GenerateAsync();

        // Assert
        filePath.ShouldNotBeNull();
        var content = await File.ReadAllTextAsync(filePath);
        content.ShouldContain("---");
        content.ShouldContain("*Generated by d365fo-data-migration*");
    }

    [Fact]
    public async Task GenerateAsync_CompletesInUnderOneSecond()
    {
        // Arrange -- create 10 cycles with 50 entities each
        var cycles = Enumerable.Range(0, 10).Select(c =>
            CreateCycle($"cycle-{c}",
                DateTimeOffset.UtcNow.AddDays(-10 + c),
                Enumerable.Range(0, 50).Select(e =>
                    CreateEntityResult($"Entity{e}", (c + e) % 7)).ToArray()
            )).ToArray();
        SetupRepository(cycles);

        // Act
        var sw = Stopwatch.StartNew();
        await _service.GenerateAsync();
        sw.Stop();

        // Assert
        sw.ElapsedMilliseconds.ShouldBeLessThan(1000);
    }

    [Fact]
    public async Task GenerateAsync_EmptyReportOutputDir_FallsBackToDestinationSettings()
    {
        // Arrange
        var emptyReportSettings = Options.Create(new ReportSettings
        {
            DefaultCycleRange = 5,
            SuccessThreshold = 0,
            WarningThreshold = 5,
            OutputDirectory = ""
        });

        var destDir = Path.Combine(_tempDir, "dest");
        Directory.CreateDirectory(destDir);
        var destSettings = Options.Create(new DestinationSettings
        {
            OutputDirectory = destDir
        });

        var service = new ReadinessReportService(
            _repository,
            emptyReportSettings,
            destSettings,
            NullLogger<ReadinessReportService>.Instance);

        var cycle = CreateCycle("cycle-1", DateTimeOffset.UtcNow,
            CreateEntityResult("Entity1", 0));
        SetupRepository(cycle);

        // Act
        var filePath = await service.GenerateAsync();

        // Assert
        filePath.ShouldNotBeNull();
        filePath.ShouldStartWith(destDir);
        filePath.ShouldContain("readiness-report-");
    }

    [Fact]
    public async Task GenerateAsync_ThresholdSettings_AppliedCorrectly()
    {
        // Arrange -- custom thresholds: success <= 2, warning <= 8
        var customReportSettings = Options.Create(new ReportSettings
        {
            DefaultCycleRange = 5,
            SuccessThreshold = 2,
            WarningThreshold = 8,
            OutputDirectory = _tempDir
        });

        var service = new ReadinessReportService(
            _repository,
            customReportSettings,
            _destinationSettings,
            NullLogger<ReadinessReportService>.Instance);

        var cycle = CreateCycle("cycle-1", DateTimeOffset.UtcNow,
            CreateEntityResult("SuccessEntity", 2),   // <= 2 -> success
            CreateEntityResult("WarningEntity", 5),   // > 2 and <= 8 -> warning
            CreateEntityResult("FailureEntity", 10)); // > 8 -> failure
        SetupRepository(cycle);

        // Act
        var filePath = await service.GenerateAsync();

        // Assert
        filePath.ShouldNotBeNull();
        var content = await File.ReadAllTextAsync(filePath);
        content.ShouldContain("### `SuccessEntity` - pass");
        content.ShouldContain("### `WarningEntity` - warn");
        content.ShouldContain("### `FailureEntity` - FAIL");
        content.ShouldContain("| Entities at Success | 1 |");
        content.ShouldContain("| Entities at Warning | 1 |");
        content.ShouldContain("| Entities at Failure | 1 |");
    }
}
