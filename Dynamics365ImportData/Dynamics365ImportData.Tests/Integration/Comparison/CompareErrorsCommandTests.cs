namespace Dynamics365ImportData.Tests.Integration.Comparison;

using Dynamics365ImportData.Comparison;
using Dynamics365ImportData.Comparison.Models;
using Dynamics365ImportData.DependencySorting;
using Dynamics365ImportData.Persistence;
using Dynamics365ImportData.Persistence.Models;
using Dynamics365ImportData.Pipeline;
using Dynamics365ImportData.Reporting;
using Dynamics365ImportData.Settings;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

using Xunit;

/// <summary>
/// Integration tests for the compare-errors standalone command and --compare flag
/// on migration commands. Tests construct a real CommandHandler with mocked services
/// to verify CLI behavior end-to-end.
/// </summary>
public class CompareErrorsCommandTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _outputDir;
    private readonly SourceQueryCollection _queries;

    public CompareErrorsCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"CompareTests_{Guid.NewGuid():N}");
        _outputDir = Path.Combine(_tempDir, "Output");
        Directory.CreateDirectory(_outputDir);

        // Create a minimal entity definition directory with required files
        string entityDir = Path.Combine(_tempDir, "TESTENTITY");
        Directory.CreateDirectory(entityDir);
        File.WriteAllText(Path.Combine(entityDir, "Manifest.xml"), "<xml/>");
        File.WriteAllText(Path.Combine(entityDir, "PackageHeader.xml"), "<xml/>");
        File.WriteAllText(Path.Combine(entityDir, "TESTENTITY.sql"), "SELECT 1");

        var sourceSettings = Options.Create(new SourceSettings
        {
            SourceConnectionString = "Server=test;Database=test;"
        });
        var destinationSettings = Options.Create(new DestinationSettings
        {
            OutputDirectory = _outputDir
        });
        var processSettings = Options.Create(new ProcessSettings
        {
            DefinitionDirectory = _tempDir,
            MaxDegreeOfParallelism = 1,
            Queries = new List<QuerySettings>
            {
                new() { EntityName = "TestEntity" }
            }
        });

        _queries = new SourceQueryCollection(
            sourceSettings,
            destinationSettings,
            processSettings,
            NullLogger<SourceQueryCollection>.Instance);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); }
        catch { /* cleanup best-effort */ }
    }

    private CommandHandler CreateCommandHandler(
        IMigrationPipelineService? mockPipeline = null,
        IErrorComparisonService? mockComparison = null,
        IErrorComparisonReportService? mockReport = null)
    {
        var pipeline = mockPipeline ?? Substitute.For<IMigrationPipelineService>();
        var comparison = mockComparison ?? Substitute.For<IErrorComparisonService>();
        var report = mockReport ?? Substitute.For<IErrorComparisonReportService>();
        var resultRepo = Substitute.For<IMigrationResultRepository>();
        var readinessService = Substitute.For<IReadinessReportService>();

        return new CommandHandler(
            pipeline,
            _queries,
            resultRepo,
            comparison,
            report,
            readinessService,
            NullLogger<CommandHandler>.Instance);
    }

    private static ComparisonResult CreateNormalComparison()
    {
        return new ComparisonResult
        {
            CurrentCycleId = "cycle-curr",
            PreviousCycleId = "cycle-prev",
            Timestamp = DateTimeOffset.UtcNow,
            IsFirstCycle = false,
            TotalNewErrors = 2,
            TotalCarryOverErrors = 1,
            TotalResolvedErrors = 1,
            EntityComparisons = new List<EntityComparisonResult>
            {
                new()
                {
                    EntityName = "Customers",
                    CurrentStatus = EntityStatus.Failed,
                    NewErrors = new List<ClassifiedError>
                    {
                        new() { EntityName = "Customers", Message = "Error 1", Fingerprint = "aabb000000000001", Classification = ErrorClassification.New },
                        new() { EntityName = "Customers", Message = "Error 2", Fingerprint = "aabb000000000002", Classification = ErrorClassification.New }
                    },
                    CarryOverErrors = new List<ClassifiedError>
                    {
                        new() { EntityName = "Customers", Message = "Old error", Fingerprint = "ccdd000000000003", Classification = ErrorClassification.CarryOver }
                    },
                    ResolvedFingerprints = new List<string> { "eeff000000000004" }
                }
            }
        };
    }

    private static ComparisonResult CreateFirstCycleComparison()
    {
        return new ComparisonResult
        {
            CurrentCycleId = "cycle-first",
            PreviousCycleId = null,
            Timestamp = DateTimeOffset.UtcNow,
            IsFirstCycle = true,
            EntityComparisons = new List<EntityComparisonResult>()
        };
    }

    #region compare-errors standalone command tests

    [Fact]
    public async Task RunCompareErrorsAsync_TwoCyclesExist_GeneratesReportAndReturnsZero()
    {
        // Arrange
        var mockComparison = Substitute.For<IErrorComparisonService>();
        mockComparison.CompareAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(CreateNormalComparison());

        var mockReport = Substitute.For<IErrorComparisonReportService>();
        mockReport.GenerateReportAsync(Arg.Any<ComparisonResult>(), null, Arg.Any<CancellationToken>())
            .Returns(Path.Combine(_outputDir, "report.md"));

        var handler = CreateCommandHandler(mockComparison: mockComparison, mockReport: mockReport);

        // Act
        var exitCode = await handler.RunCompareErrorsAsync(cancellationToken: CancellationToken.None);

        // Assert
        exitCode.ShouldBe(0);
        await mockComparison.Received(1).CompareAsync(null, null, Arg.Any<CancellationToken>());
        await mockReport.Received(1).GenerateReportAsync(Arg.Any<ComparisonResult>(), null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunCompareErrorsAsync_FirstCycle_GeneratesFirstCycleReportAndReturnsZero()
    {
        // Arrange
        var mockComparison = Substitute.For<IErrorComparisonService>();
        mockComparison.CompareAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(CreateFirstCycleComparison());

        var mockReport = Substitute.For<IErrorComparisonReportService>();
        mockReport.GenerateReportAsync(Arg.Any<ComparisonResult>(), null, Arg.Any<CancellationToken>())
            .Returns(Path.Combine(_outputDir, "report.md"));

        var handler = CreateCommandHandler(mockComparison: mockComparison, mockReport: mockReport);

        // Act
        var exitCode = await handler.RunCompareErrorsAsync(cancellationToken: CancellationToken.None);

        // Assert
        exitCode.ShouldBe(0);
    }

    [Fact]
    public async Task RunCompareErrorsAsync_NoCycles_ReturnsOne()
    {
        // Arrange -- real ErrorComparisonService returns IsFirstCycle=true with empty
        // CurrentCycleId when no cycles exist (it does NOT throw an exception)
        var mockComparison = Substitute.For<IErrorComparisonService>();
        mockComparison.CompareAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new ComparisonResult
            {
                IsFirstCycle = true,
                Timestamp = DateTimeOffset.UtcNow
                // CurrentCycleId defaults to string.Empty -- signals no cycles
            });

        var handler = CreateCommandHandler(mockComparison: mockComparison);

        // Act
        var exitCode = await handler.RunCompareErrorsAsync(cancellationToken: CancellationToken.None);

        // Assert
        exitCode.ShouldBe(1);
    }

    [Fact]
    public async Task RunCompareErrorsAsync_WithCycleOption_PassesCycleIdToService()
    {
        // Arrange
        var mockComparison = Substitute.For<IErrorComparisonService>();
        mockComparison.CompareAsync(null, "cycle-specific", Arg.Any<CancellationToken>())
            .Returns(CreateNormalComparison());

        var mockReport = Substitute.For<IErrorComparisonReportService>();
        mockReport.GenerateReportAsync(Arg.Any<ComparisonResult>(), null, Arg.Any<CancellationToken>())
            .Returns(Path.Combine(_outputDir, "report.md"));

        var handler = CreateCommandHandler(mockComparison: mockComparison, mockReport: mockReport);

        // Act
        var exitCode = await handler.RunCompareErrorsAsync(cycleId: "cycle-specific", cancellationToken: CancellationToken.None);

        // Assert
        exitCode.ShouldBe(0);
        await mockComparison.Received(1).CompareAsync(null, "cycle-specific", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunCompareErrorsAsync_WithOutputOption_PassesOutputPathToService()
    {
        // Arrange
        var customOutput = Path.Combine(_outputDir, "custom-report.md");
        var mockComparison = Substitute.For<IErrorComparisonService>();
        mockComparison.CompareAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(CreateNormalComparison());

        var mockReport = Substitute.For<IErrorComparisonReportService>();
        mockReport.GenerateReportAsync(Arg.Any<ComparisonResult>(), customOutput, Arg.Any<CancellationToken>())
            .Returns(customOutput);

        var handler = CreateCommandHandler(mockComparison: mockComparison, mockReport: mockReport);

        // Act
        var exitCode = await handler.RunCompareErrorsAsync(outputPath: customOutput, cancellationToken: CancellationToken.None);

        // Assert
        exitCode.ShouldBe(0);
        await mockReport.Received(1).GenerateReportAsync(Arg.Any<ComparisonResult>(), customOutput, Arg.Any<CancellationToken>());
    }

    #endregion

    #region --compare flag tests

    [Fact]
    public async Task CompareFlag_AfterMigration_TriggersReportGeneration()
    {
        // Arrange
        var mockPipeline = Substitute.For<IMigrationPipelineService>();
        mockPipeline.ExecuteAsync(Arg.Any<PipelineMode>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(new CycleResult { Command = "File", TotalEntities = 1, Succeeded = 1, Failed = 0 });

        var mockComparison = Substitute.For<IErrorComparisonService>();
        mockComparison.CompareAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(CreateNormalComparison());

        var mockReport = Substitute.For<IErrorComparisonReportService>();
        mockReport.GenerateReportAsync(Arg.Any<ComparisonResult>(), null, Arg.Any<CancellationToken>())
            .Returns(Path.Combine(_outputDir, "report.md"));

        var handler = CreateCommandHandler(
            mockPipeline: mockPipeline,
            mockComparison: mockComparison,
            mockReport: mockReport);

        // Act
        var exitCode = await handler.RunExportToFileAsync(compare: true, cancellationToken: CancellationToken.None);

        // Assert
        exitCode.ShouldBe(0);
        await mockComparison.Received(1).CompareAsync(null, null, Arg.Any<CancellationToken>());
        await mockReport.Received(1).GenerateReportAsync(Arg.Any<ComparisonResult>(), null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompareFlag_ReportFailure_DoesNotChangeExitCode()
    {
        // Arrange
        var mockPipeline = Substitute.For<IMigrationPipelineService>();
        mockPipeline.ExecuteAsync(Arg.Any<PipelineMode>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(new CycleResult { Command = "File", TotalEntities = 1, Succeeded = 1, Failed = 0 });

        var mockComparison = Substitute.For<IErrorComparisonService>();
        mockComparison.CompareAsync(null, null, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Report generation failed"));

        var handler = CreateCommandHandler(
            mockPipeline: mockPipeline,
            mockComparison: mockComparison);

        // Act
        var exitCode = await handler.RunExportToFileAsync(compare: true, cancellationToken: CancellationToken.None);

        // Assert -- migration succeeded, report failure is non-fatal
        exitCode.ShouldBe(0);
    }

    #endregion
}
