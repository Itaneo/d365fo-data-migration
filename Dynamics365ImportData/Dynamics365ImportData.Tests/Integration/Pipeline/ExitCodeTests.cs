namespace Dynamics365ImportData.Tests.Integration.Pipeline;

using Dynamics365ImportData.DependencySorting;
using Dynamics365ImportData.Pipeline;
using Dynamics365ImportData.Settings;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

using Xunit;

/// <summary>
/// Integration tests for exit code standardization across all CommandHandler commands.
/// Verifies ADR-8 exit code contract: 0 = all succeed, 1 = partial failure or
/// cancellation or general exception, 2 = entity validation failure.
/// Tests construct a real CommandHandler with a real SourceQueryCollection
/// (backed by temp files) and a mocked IMigrationPipelineService.
/// Located in Integration/ because SourceQueryCollection requires real filesystem
/// artifacts (temp directories with entity definition files).
/// </summary>
public class ExitCodeTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _outputDir;
    private readonly SourceQueryCollection _queries;

    public ExitCodeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ExitCodeTests_{Guid.NewGuid():N}");
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

    private CommandHandler CreateCommandHandler(IMigrationPipelineService mockPipeline)
    {
        return new CommandHandler(
            mockPipeline,
            _queries,
            NullLogger<CommandHandler>.Instance);
    }

    #region export-file exit code tests

    [Fact]
    public async Task RunExportToFileAsync_AllEntitiesSucceed_ReturnsExitCode0()
    {
        // Arrange
        var mockPipeline = Substitute.For<IMigrationPipelineService>();
        mockPipeline.ExecuteAsync(Arg.Any<PipelineMode>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CycleResult { Command = "File", TotalEntities = 3, Succeeded = 3, Failed = 0 }));
        var handler = CreateCommandHandler(mockPipeline);

        // Act
        var exitCode = await handler.RunExportToFileAsync(cancellationToken: CancellationToken.None);

        // Assert
        exitCode.ShouldBe(0);
    }

    [Fact]
    public async Task RunExportToFileAsync_SomeEntitiesFail_ReturnsExitCode1()
    {
        // Arrange
        var mockPipeline = Substitute.For<IMigrationPipelineService>();
        mockPipeline.ExecuteAsync(Arg.Any<PipelineMode>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CycleResult { Command = "File", TotalEntities = 3, Succeeded = 2, Failed = 1 }));
        var handler = CreateCommandHandler(mockPipeline);

        // Act
        var exitCode = await handler.RunExportToFileAsync(cancellationToken: CancellationToken.None);

        // Assert
        exitCode.ShouldBe(1);
    }

    [Fact]
    public async Task RunExportToFileAsync_EntityValidationFails_ReturnsExitCode2()
    {
        // Arrange
        var mockPipeline = Substitute.For<IMigrationPipelineService>();
        mockPipeline.ExecuteAsync(Arg.Any<PipelineMode>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new EntityValidationException(
                new List<string> { "BadEntity" },
                new HashSet<string> { "TESTENTITY" }));
        var handler = CreateCommandHandler(mockPipeline);

        // Act
        var exitCode = await handler.RunExportToFileAsync(cancellationToken: CancellationToken.None);

        // Assert
        exitCode.ShouldBe(2);
    }

    [Fact]
    public async Task RunExportToFileAsync_OperationCanceled_ReturnsExitCode1()
    {
        // Arrange
        var mockPipeline = Substitute.For<IMigrationPipelineService>();
        mockPipeline.ExecuteAsync(Arg.Any<PipelineMode>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());
        var handler = CreateCommandHandler(mockPipeline);

        // Act
        var exitCode = await handler.RunExportToFileAsync(cancellationToken: CancellationToken.None);

        // Assert
        exitCode.ShouldBe(1);
    }

    [Fact]
    public async Task RunExportToFileAsync_GeneralException_ReturnsExitCode1()
    {
        // Arrange
        var mockPipeline = Substitute.For<IMigrationPipelineService>();
        mockPipeline.ExecuteAsync(Arg.Any<PipelineMode>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Something went wrong"));
        var handler = CreateCommandHandler(mockPipeline);

        // Act
        var exitCode = await handler.RunExportToFileAsync(cancellationToken: CancellationToken.None);

        // Assert
        exitCode.ShouldBe(1);
    }

    #endregion

    #region export-package exit code tests

    [Fact]
    public async Task RunExportToPackageAsync_AllEntitiesSucceed_ReturnsExitCode0()
    {
        // Arrange
        var mockPipeline = Substitute.For<IMigrationPipelineService>();
        mockPipeline.ExecuteAsync(Arg.Any<PipelineMode>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CycleResult { Command = "Package", TotalEntities = 3, Succeeded = 3, Failed = 0 }));
        var handler = CreateCommandHandler(mockPipeline);

        // Act
        var exitCode = await handler.RunExportToPackageAsync(cancellationToken: CancellationToken.None);

        // Assert
        exitCode.ShouldBe(0);
    }

    [Fact]
    public async Task RunExportToPackageAsync_SomeEntitiesFail_ReturnsExitCode1()
    {
        // Arrange
        var mockPipeline = Substitute.For<IMigrationPipelineService>();
        mockPipeline.ExecuteAsync(Arg.Any<PipelineMode>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CycleResult { Command = "Package", TotalEntities = 3, Succeeded = 2, Failed = 1 }));
        var handler = CreateCommandHandler(mockPipeline);

        // Act
        var exitCode = await handler.RunExportToPackageAsync(cancellationToken: CancellationToken.None);

        // Assert
        exitCode.ShouldBe(1);
    }

    [Fact]
    public async Task RunExportToPackageAsync_EntityValidationFails_ReturnsExitCode2()
    {
        // Arrange
        var mockPipeline = Substitute.For<IMigrationPipelineService>();
        mockPipeline.ExecuteAsync(Arg.Any<PipelineMode>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new EntityValidationException(
                new List<string> { "BadEntity" },
                new HashSet<string> { "TESTENTITY" }));
        var handler = CreateCommandHandler(mockPipeline);

        // Act
        var exitCode = await handler.RunExportToPackageAsync(cancellationToken: CancellationToken.None);

        // Assert
        exitCode.ShouldBe(2);
    }

    [Fact]
    public async Task RunExportToPackageAsync_OperationCanceled_ReturnsExitCode1()
    {
        // Arrange
        var mockPipeline = Substitute.For<IMigrationPipelineService>();
        mockPipeline.ExecuteAsync(Arg.Any<PipelineMode>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());
        var handler = CreateCommandHandler(mockPipeline);

        // Act
        var exitCode = await handler.RunExportToPackageAsync(cancellationToken: CancellationToken.None);

        // Assert
        exitCode.ShouldBe(1);
    }

    [Fact]
    public async Task RunExportToPackageAsync_GeneralException_ReturnsExitCode1()
    {
        // Arrange
        var mockPipeline = Substitute.For<IMigrationPipelineService>();
        mockPipeline.ExecuteAsync(Arg.Any<PipelineMode>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Connection failed"));
        var handler = CreateCommandHandler(mockPipeline);

        // Act
        var exitCode = await handler.RunExportToPackageAsync(cancellationToken: CancellationToken.None);

        // Assert
        exitCode.ShouldBe(1);
    }

    #endregion

    #region import-d365 exit code tests

    [Fact]
    public async Task RunImportDynamicsAsync_AllEntitiesSucceed_ReturnsExitCode0()
    {
        // Arrange
        var mockPipeline = Substitute.For<IMigrationPipelineService>();
        mockPipeline.ExecuteAsync(Arg.Any<PipelineMode>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CycleResult { Command = "D365", TotalEntities = 3, Succeeded = 3, Failed = 0 }));
        var handler = CreateCommandHandler(mockPipeline);

        // Act
        var exitCode = await handler.RunImportDynamicsAsync(cancellationToken: CancellationToken.None);

        // Assert
        exitCode.ShouldBe(0);
    }

    [Fact]
    public async Task RunImportDynamicsAsync_SomeEntitiesFail_ReturnsExitCode1()
    {
        // Arrange
        var mockPipeline = Substitute.For<IMigrationPipelineService>();
        mockPipeline.ExecuteAsync(Arg.Any<PipelineMode>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CycleResult { Command = "D365", TotalEntities = 3, Succeeded = 2, Failed = 1 }));
        var handler = CreateCommandHandler(mockPipeline);

        // Act
        var exitCode = await handler.RunImportDynamicsAsync(cancellationToken: CancellationToken.None);

        // Assert
        exitCode.ShouldBe(1);
    }

    [Fact]
    public async Task RunImportDynamicsAsync_EntityValidationFails_ReturnsExitCode2()
    {
        // Arrange
        var mockPipeline = Substitute.For<IMigrationPipelineService>();
        mockPipeline.ExecuteAsync(Arg.Any<PipelineMode>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new EntityValidationException(
                new List<string> { "BadEntity" },
                new HashSet<string> { "TESTENTITY" }));
        var handler = CreateCommandHandler(mockPipeline);

        // Act
        var exitCode = await handler.RunImportDynamicsAsync(cancellationToken: CancellationToken.None);

        // Assert
        exitCode.ShouldBe(2);
    }

    [Fact]
    public async Task RunImportDynamicsAsync_OperationCanceled_ReturnsExitCode1()
    {
        // Arrange
        var mockPipeline = Substitute.For<IMigrationPipelineService>();
        mockPipeline.ExecuteAsync(Arg.Any<PipelineMode>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());
        var handler = CreateCommandHandler(mockPipeline);

        // Act
        var exitCode = await handler.RunImportDynamicsAsync(cancellationToken: CancellationToken.None);

        // Assert
        exitCode.ShouldBe(1);
    }

    [Fact]
    public async Task RunImportDynamicsAsync_GeneralException_ReturnsExitCode1()
    {
        // Arrange
        var mockPipeline = Substitute.For<IMigrationPipelineService>();
        mockPipeline.ExecuteAsync(Arg.Any<PipelineMode>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Connection failed"));
        var handler = CreateCommandHandler(mockPipeline);

        // Act
        var exitCode = await handler.RunImportDynamicsAsync(cancellationToken: CancellationToken.None);

        // Assert
        exitCode.ShouldBe(1);
    }

    #endregion

    #region Cross-command consistency tests

    [Fact]
    public async Task AllCommands_TaskCanceledException_ReturnsExitCode1()
    {
        // Arrange -- TaskCanceledException inherits from OperationCanceledException.
        // Each command gets a fresh handler to avoid ClearOutputDirectory() side effects.
        var mockPipeline = Substitute.For<IMigrationPipelineService>();
        mockPipeline.ExecuteAsync(Arg.Any<PipelineMode>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("HTTP request timed out"));

        // Act
        var fileExit = await CreateCommandHandler(mockPipeline).RunExportToFileAsync(cancellationToken: CancellationToken.None);
        var packageExit = await CreateCommandHandler(mockPipeline).RunExportToPackageAsync(cancellationToken: CancellationToken.None);
        var importExit = await CreateCommandHandler(mockPipeline).RunImportDynamicsAsync(cancellationToken: CancellationToken.None);

        // Assert -- all commands should return 1 for cancellation
        fileExit.ShouldBe(1);
        packageExit.ShouldBe(1);
        importExit.ShouldBe(1);
    }

    #endregion
}
