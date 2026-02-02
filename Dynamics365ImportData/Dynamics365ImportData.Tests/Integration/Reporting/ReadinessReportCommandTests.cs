namespace Dynamics365ImportData.Tests.Integration.Reporting;

using Dynamics365ImportData.Comparison;
using Dynamics365ImportData.DependencySorting;
using Dynamics365ImportData.Persistence;
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
/// Integration tests for the readiness-report / rr CLI command.
/// Tests construct a real CommandHandler with mocked services to verify
/// CLI behavior, exit codes, and parameter passing end-to-end.
/// </summary>
public class ReadinessReportCommandTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _outputDir;
    private readonly SourceQueryCollection _queries;

    public ReadinessReportCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ReadinessReportCmdTests_{Guid.NewGuid():N}");
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

    private CommandHandler CreateCommandHandler(IReadinessReportService? mockReadiness = null)
    {
        var pipeline = Substitute.For<IMigrationPipelineService>();
        var comparison = Substitute.For<IErrorComparisonService>();
        var report = Substitute.For<IErrorComparisonReportService>();
        var resultRepo = Substitute.For<IMigrationResultRepository>();
        var readiness = mockReadiness ?? Substitute.For<IReadinessReportService>();

        return new CommandHandler(
            pipeline,
            _queries,
            resultRepo,
            comparison,
            report,
            readiness,
            NullLogger<CommandHandler>.Instance);
    }

    #region Exit code tests

    [Fact]
    public async Task RunReadinessReportAsync_SuccessfulGeneration_ReturnsExitCode0()
    {
        // Arrange
        var mockReadiness = Substitute.For<IReadinessReportService>();
        mockReadiness.GenerateAsync(
                Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>(Path.Combine(_outputDir, "readiness-report.md")));
        var handler = CreateCommandHandler(mockReadiness);

        // Act
        var exitCode = await handler.RunReadinessReportAsync(cancellationToken: CancellationToken.None);

        // Assert
        exitCode.ShouldBe(0);
    }

    [Fact]
    public async Task RunReadinessReportAsync_NoCyclesFound_ReturnsExitCode1()
    {
        // Arrange
        var mockReadiness = Substitute.For<IReadinessReportService>();
        mockReadiness.GenerateAsync(
                Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>(null));
        var handler = CreateCommandHandler(mockReadiness);

        // Act
        var exitCode = await handler.RunReadinessReportAsync(cancellationToken: CancellationToken.None);

        // Assert
        exitCode.ShouldBe(1);
    }

    [Fact]
    public async Task RunReadinessReportAsync_Canceled_ReturnsExitCode1()
    {
        // Arrange
        var mockReadiness = Substitute.For<IReadinessReportService>();
        mockReadiness.GenerateAsync(
                Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());
        var handler = CreateCommandHandler(mockReadiness);

        // Act
        var exitCode = await handler.RunReadinessReportAsync(cancellationToken: CancellationToken.None);

        // Assert
        exitCode.ShouldBe(1);
    }

    [Fact]
    public async Task RunReadinessReportAsync_ServiceException_ReturnsExitCode1()
    {
        // Arrange
        var mockReadiness = Substitute.For<IReadinessReportService>();
        mockReadiness.GenerateAsync(
                Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Something went wrong"));
        var handler = CreateCommandHandler(mockReadiness);

        // Act
        var exitCode = await handler.RunReadinessReportAsync(cancellationToken: CancellationToken.None);

        // Assert
        exitCode.ShouldBe(1);
    }

    [Fact]
    public async Task RunReadinessReportAsync_ZeroCycles_ReturnsExitCode2()
    {
        // Arrange
        var handler = CreateCommandHandler();

        // Act
        var exitCode = await handler.RunReadinessReportAsync(cycles: 0, cancellationToken: CancellationToken.None);

        // Assert
        exitCode.ShouldBe(2);
    }

    [Fact]
    public async Task RunReadinessReportAsync_NegativeCycles_ReturnsExitCode2()
    {
        // Arrange
        var handler = CreateCommandHandler();

        // Act
        var exitCode = await handler.RunReadinessReportAsync(cycles: -3, cancellationToken: CancellationToken.None);

        // Assert
        exitCode.ShouldBe(2);
    }

    [Fact]
    public async Task RunReadinessReportAsync_InvalidThreshold_ReturnsExitCode2()
    {
        // Arrange
        var handler = CreateCommandHandler();

        // Act
        var exitCode = await handler.RunReadinessReportAsync(
            thresholdConfig: "invalid-format", cancellationToken: CancellationToken.None);

        // Assert
        exitCode.ShouldBe(2);
    }

    #endregion

    #region Parameter passing tests

    [Fact]
    public async Task RunReadinessReportAsync_DefaultCycles_PassesFiveToService()
    {
        // Arrange
        var mockReadiness = Substitute.For<IReadinessReportService>();
        mockReadiness.GenerateAsync(
                Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>(Path.Combine(_outputDir, "report.md")));
        var handler = CreateCommandHandler(mockReadiness);

        // Act
        await handler.RunReadinessReportAsync(cancellationToken: CancellationToken.None);

        // Assert
        await mockReadiness.Received(1).GenerateAsync(
            5, null, null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunReadinessReportAsync_CustomCycles_PassesValueToService()
    {
        // Arrange
        var mockReadiness = Substitute.For<IReadinessReportService>();
        mockReadiness.GenerateAsync(
                Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>(Path.Combine(_outputDir, "report.md")));
        var handler = CreateCommandHandler(mockReadiness);

        // Act
        await handler.RunReadinessReportAsync(cycles: 10, cancellationToken: CancellationToken.None);

        // Assert
        await mockReadiness.Received(1).GenerateAsync(
            10, null, null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunReadinessReportAsync_CustomOutputPath_PassesPathToService()
    {
        // Arrange
        var customOutput = Path.Combine(_outputDir, "custom-readiness.md");
        var mockReadiness = Substitute.For<IReadinessReportService>();
        mockReadiness.GenerateAsync(
                Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>(customOutput));
        var handler = CreateCommandHandler(mockReadiness);

        // Act
        await handler.RunReadinessReportAsync(outputPath: customOutput, cancellationToken: CancellationToken.None);

        // Assert
        await mockReadiness.Received(1).GenerateAsync(
            5, customOutput, null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunReadinessReportAsync_ValidThreshold_ParsesCorrectly()
    {
        // Arrange
        var mockReadiness = Substitute.For<IReadinessReportService>();
        mockReadiness.GenerateAsync(
                Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>(Path.Combine(_outputDir, "report.md")));
        var handler = CreateCommandHandler(mockReadiness);

        // Act
        await handler.RunReadinessReportAsync(
            thresholdConfig: "success:0,warning:10", cancellationToken: CancellationToken.None);

        // Assert
        await mockReadiness.Received(1).GenerateAsync(
            5, null, 0, 10, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunReadinessReportAsync_SuccessOnlyThreshold_PassesSuccessToService()
    {
        // Arrange
        var mockReadiness = Substitute.For<IReadinessReportService>();
        mockReadiness.GenerateAsync(
                Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>(Path.Combine(_outputDir, "report.md")));
        var handler = CreateCommandHandler(mockReadiness);

        // Act
        await handler.RunReadinessReportAsync(
            thresholdConfig: "success:5", cancellationToken: CancellationToken.None);

        // Assert -- success threshold parsed, warning threshold null
        await mockReadiness.Received(1).GenerateAsync(
            5, null, 5, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunReadinessReportAsync_WarningOnlyThreshold_PassesWarningToService()
    {
        // Arrange
        var mockReadiness = Substitute.For<IReadinessReportService>();
        mockReadiness.GenerateAsync(
                Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>(Path.Combine(_outputDir, "report.md")));
        var handler = CreateCommandHandler(mockReadiness);

        // Act
        await handler.RunReadinessReportAsync(
            thresholdConfig: "warning:3", cancellationToken: CancellationToken.None);

        // Assert -- success threshold null, warning threshold parsed
        await mockReadiness.Received(1).GenerateAsync(
            5, null, null, 3, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunReadinessReportAsync_NonNumericThreshold_ReturnsExitCode2()
    {
        // Arrange
        var handler = CreateCommandHandler();

        // Act
        var exitCode = await handler.RunReadinessReportAsync(
            thresholdConfig: "success:abc", cancellationToken: CancellationToken.None);

        // Assert
        exitCode.ShouldBe(2);
    }

    #endregion
}
