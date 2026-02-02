namespace Dynamics365ImportData.Tests.Unit.Comparison;

using System.Diagnostics;

using Dynamics365ImportData.Comparison;
using Dynamics365ImportData.Comparison.Models;
using Dynamics365ImportData.Persistence.Models;
using Dynamics365ImportData.Settings;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

public class ErrorComparisonReportServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ErrorComparisonReportService _service;

    public ErrorComparisonReportServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ReportTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        var settings = Options.Create(new DestinationSettings
        {
            OutputDirectory = _tempDir
        });

        _service = new ErrorComparisonReportService(
            settings,
            NullLogger<ErrorComparisonReportService>.Instance);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); }
        catch { /* cleanup best-effort */ }
    }

    private static ComparisonResult CreateComparisonResult(
        string currentCycleId = "cycle-curr",
        string? previousCycleId = "cycle-prev",
        bool isFirstCycle = false,
        int totalNew = 0,
        int totalCarryOver = 0,
        int totalResolved = 0,
        params EntityComparisonResult[] entities)
    {
        return new ComparisonResult
        {
            CurrentCycleId = currentCycleId,
            PreviousCycleId = previousCycleId,
            Timestamp = new DateTimeOffset(2026, 2, 1, 14, 30, 0, TimeSpan.Zero),
            IsFirstCycle = isFirstCycle,
            TotalNewErrors = totalNew,
            TotalCarryOverErrors = totalCarryOver,
            TotalResolvedErrors = totalResolved,
            EntityComparisons = entities.ToList()
        };
    }

    private static EntityComparisonResult CreateEntityComparison(
        string entityName,
        EntityStatus status,
        List<ClassifiedError>? newErrors = null,
        List<ClassifiedError>? carryOverErrors = null,
        List<string>? resolvedFingerprints = null)
    {
        return new EntityComparisonResult
        {
            EntityName = entityName,
            CurrentStatus = status,
            NewErrors = newErrors ?? new List<ClassifiedError>(),
            CarryOverErrors = carryOverErrors ?? new List<ClassifiedError>(),
            ResolvedFingerprints = resolvedFingerprints ?? new List<string>()
        };
    }

    private static ClassifiedError CreateClassifiedError(
        string entityName,
        string message,
        string fingerprint,
        ErrorClassification classification)
    {
        return new ClassifiedError
        {
            EntityName = entityName,
            Message = message,
            Fingerprint = fingerprint,
            Classification = classification,
            Category = ErrorCategory.Technical
        };
    }

    [Fact]
    public async Task GenerateReportAsync_WithComparison_WritesMarkdownFile()
    {
        // Arrange
        var comparison = CreateComparisonResult(totalNew: 1, totalCarryOver: 0, totalResolved: 0,
            entities: new[] { CreateEntityComparison("Customers", EntityStatus.Failed,
                newErrors: new List<ClassifiedError>
                {
                    CreateClassifiedError("Customers", "Error msg", "aabb000000000001", ErrorClassification.New)
                }) });

        // Act
        var filePath = await _service.GenerateReportAsync(comparison);

        // Assert
        File.Exists(filePath).ShouldBeTrue();
        var content = await File.ReadAllTextAsync(filePath);
        content.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GenerateReportAsync_ReportContainsHeader_WithCycleIds()
    {
        // Arrange
        var comparison = CreateComparisonResult(
            currentCycleId: "cycle-2026-02-01",
            previousCycleId: "cycle-2026-01-31");

        // Act
        var filePath = await _service.GenerateReportAsync(comparison);

        // Assert
        var content = await File.ReadAllTextAsync(filePath);
        content.ShouldContain("# Error Comparison Report");
        content.ShouldContain("**Generated:**");
        content.ShouldContain("**Current Cycle:** cycle-2026-02-01");
        content.ShouldContain("**Previous Cycle:** cycle-2026-01-31");
    }

    [Fact]
    public async Task GenerateReportAsync_ReportContainsSummaryTable_WithCorrectCounts()
    {
        // Arrange
        var comparison = CreateComparisonResult(totalNew: 3, totalCarryOver: 5, totalResolved: 2);

        // Act
        var filePath = await _service.GenerateReportAsync(comparison);

        // Assert
        var content = await File.ReadAllTextAsync(filePath);
        content.ShouldContain("## Summary");
        content.ShouldContain("| New Errors | 3 |");
        content.ShouldContain("| Carry-Over Errors | 5 |");
        content.ShouldContain("| Resolved Errors | 2 |");
        content.ShouldContain("| Total Current Errors | 8 |");
    }

    [Fact]
    public async Task GenerateReportAsync_ReportContainsEntityDetails_WithCodeFormatting()
    {
        // Arrange
        var comparison = CreateComparisonResult(totalNew: 1, totalCarryOver: 0, totalResolved: 0,
            entities: new[] { CreateEntityComparison("CustCustomerV3Entity", EntityStatus.Failed,
                newErrors: new List<ClassifiedError>
                {
                    CreateClassifiedError("CustCustomerV3Entity", "Error", "aabb000000000001", ErrorClassification.New)
                }) });

        // Act
        var filePath = await _service.GenerateReportAsync(comparison);

        // Assert
        var content = await File.ReadAllTextAsync(filePath);
        content.ShouldContain("### `CustCustomerV3Entity` - FAIL");
    }

    [Fact]
    public async Task GenerateReportAsync_ReportContainsStatusIndicators_CorrectFormat()
    {
        // Arrange
        var comparison = CreateComparisonResult(totalNew: 1, totalCarryOver: 0, totalResolved: 0,
            entities: new[] {
                CreateEntityComparison("FailEntity", EntityStatus.Failed,
                    newErrors: new List<ClassifiedError>
                    {
                        CreateClassifiedError("FailEntity", "Error", "aabb000000000001", ErrorClassification.New)
                    }),
                CreateEntityComparison("PassEntity", EntityStatus.Success,
                    resolvedFingerprints: new List<string> { "ccdd000000000002" }),
                CreateEntityComparison("WarnEntity", EntityStatus.Warning,
                    newErrors: new List<ClassifiedError>
                    {
                        CreateClassifiedError("WarnEntity", "Warning", "eeff000000000003", ErrorClassification.New)
                    })
            });

        // Act
        var filePath = await _service.GenerateReportAsync(comparison);

        // Assert
        var content = await File.ReadAllTextAsync(filePath);
        content.ShouldContain("### `FailEntity` - FAIL");
        content.ShouldContain("### `PassEntity` - pass");
        content.ShouldContain("### `WarnEntity` - warn");
    }

    [Fact]
    public async Task GenerateReportAsync_ReportContainsNewErrors_WithFingerprints()
    {
        // Arrange
        var comparison = CreateComparisonResult(totalNew: 1, totalCarryOver: 0, totalResolved: 0,
            entities: new[] { CreateEntityComparison("Customers", EntityStatus.Failed,
                newErrors: new List<ClassifiedError>
                {
                    CreateClassifiedError("Customers", "Record validation failed", "a1b2c3d4e5f6a7b8", ErrorClassification.New)
                }) });

        // Act
        var filePath = await _service.GenerateReportAsync(comparison);

        // Assert
        var content = await File.ReadAllTextAsync(filePath);
        content.ShouldContain("**New Errors:**");
        content.ShouldContain("- `[a1b2c3d4e5f6a7b8]` Record validation failed");
    }

    [Fact]
    public async Task GenerateReportAsync_ReportContainsCarryOverErrors_WithFingerprints()
    {
        // Arrange
        var comparison = CreateComparisonResult(totalNew: 0, totalCarryOver: 1, totalResolved: 0,
            entities: new[] { CreateEntityComparison("Customers", EntityStatus.Failed,
                carryOverErrors: new List<ClassifiedError>
                {
                    CreateClassifiedError("Customers", "Foreign key violation", "1234567890abcdef", ErrorClassification.CarryOver)
                }) });

        // Act
        var filePath = await _service.GenerateReportAsync(comparison);

        // Assert
        var content = await File.ReadAllTextAsync(filePath);
        content.ShouldContain("**Carry-Over Errors:**");
        content.ShouldContain("- `[1234567890abcdef]` Foreign key violation");
    }

    [Fact]
    public async Task GenerateReportAsync_ReportContainsResolvedFingerprints()
    {
        // Arrange
        var comparison = CreateComparisonResult(totalNew: 0, totalCarryOver: 0, totalResolved: 1,
            entities: new[] { CreateEntityComparison("Customers", EntityStatus.Success,
                resolvedFingerprints: new List<string> { "9876543210fedcba" }) });

        // Act
        var filePath = await _service.GenerateReportAsync(comparison);

        // Assert
        var content = await File.ReadAllTextAsync(filePath);
        content.ShouldContain("**Resolved (from previous cycle):**");
        content.ShouldContain("- `[9876543210fedcba]`");
    }

    [Fact]
    public async Task GenerateReportAsync_FirstCycle_GeneratesFirstCycleReport()
    {
        // Arrange
        var comparison = CreateComparisonResult(
            currentCycleId: "cycle-2026-02-01T143000",
            previousCycleId: null,
            isFirstCycle: true);

        // Act
        var filePath = await _service.GenerateReportAsync(comparison);

        // Assert
        var content = await File.ReadAllTextAsync(filePath);
        content.ShouldContain("# Error Comparison Report");
        content.ShouldContain("**Current Cycle:** cycle-2026-02-01T143000");
        content.ShouldContain("First cycle -- no comparison available. Run another migration cycle to enable error comparison.");
        content.ShouldNotContain("**Previous Cycle:**");
        content.ShouldNotContain("## Entity Details");
    }

    [Fact]
    public async Task GenerateReportAsync_CustomOutputPath_WritesToSpecifiedPath()
    {
        // Arrange
        var comparison = CreateComparisonResult();
        var customPath = Path.Combine(_tempDir, "custom", "my-report.md");

        // Act
        var filePath = await _service.GenerateReportAsync(comparison, outputPath: customPath);

        // Assert
        filePath.ShouldBe(customPath);
        File.Exists(customPath).ShouldBeTrue();
    }

    [Fact]
    public async Task GenerateReportAsync_DefaultOutputPath_UsesOutputDirectory()
    {
        // Arrange
        var comparison = CreateComparisonResult(currentCycleId: "cycle-test-123");

        // Act
        var filePath = await _service.GenerateReportAsync(comparison);

        // Assert
        filePath.ShouldBe(Path.Combine(_tempDir, "error-comparison-cycle-test-123.md"));
        File.Exists(filePath).ShouldBeTrue();
    }

    [Fact]
    public async Task GenerateReportAsync_ReportContainsFooter()
    {
        // Arrange
        var comparison = CreateComparisonResult();

        // Act
        var filePath = await _service.GenerateReportAsync(comparison);

        // Assert
        var content = await File.ReadAllTextAsync(filePath);
        content.ShouldContain("---");
        content.ShouldContain("*Generated by d365fo-data-migration*");
    }

    [Fact]
    public async Task GenerateReportAsync_CompletesInUnderOneSecond()
    {
        // Arrange -- create a comparison with many entities for performance testing
        var entities = Enumerable.Range(0, 50).Select(i =>
            CreateEntityComparison($"Entity{i}", EntityStatus.Failed,
                newErrors: Enumerable.Range(0, 10).Select(j =>
                    CreateClassifiedError($"Entity{i}", $"Error {j}", $"{i:D8}{j:D8}", ErrorClassification.New)).ToList(),
                carryOverErrors: Enumerable.Range(0, 5).Select(j =>
                    CreateClassifiedError($"Entity{i}", $"CarryOver {j}", $"co{i:D6}{j:D8}", ErrorClassification.CarryOver)).ToList(),
                resolvedFingerprints: Enumerable.Range(0, 3).Select(j => $"res{i:D6}{j:D8}").ToList()
            )).ToArray();

        var comparison = CreateComparisonResult(
            totalNew: 500, totalCarryOver: 250, totalResolved: 150,
            entities: entities);

        // Act
        var sw = Stopwatch.StartNew();
        await _service.GenerateReportAsync(comparison);
        sw.Stop();

        // Assert
        sw.ElapsedMilliseconds.ShouldBeLessThan(1000);
    }
}
