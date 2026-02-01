namespace Dynamics365ImportData.Tests.Integration;

using System.Diagnostics;

using Dynamics365ImportData.DependencySorting;
using Dynamics365ImportData.Settings;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

/// <summary>
/// Integration tests for Program.Main exit code 2 behavior (ADR-8).
/// Two categories:
///   1. SourceQueryCollection validation tests: verify the constructor throws
///      the correct exceptions for config errors (precondition for exit code 2).
///   2. Process-based end-to-end test: verifies Program.Main actually returns
///      exit code 2 when a config error occurs at startup.
/// </summary>
public class ProgramExitCodeTests : IDisposable
{
    private readonly string _tempDir;

    public ProgramExitCodeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ProgramExitCodeTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); }
        catch { /* cleanup best-effort */ }
    }

    #region SourceQueryCollection validation (precondition tests)

    [Fact]
    public void SourceQueryCollection_EmptyQueries_ThrowsArgumentException()
    {
        // Arrange -- empty Queries array triggers ArgumentException in constructor
        var sourceSettings = Options.Create(new SourceSettings
        {
            SourceConnectionString = "Server=test;Database=test;"
        });
        var destinationSettings = Options.Create(new DestinationSettings
        {
            OutputDirectory = Path.GetTempPath()
        });
        var processSettings = Options.Create(new ProcessSettings
        {
            MaxDegreeOfParallelism = 1,
            Queries = new List<QuerySettings>() // Empty -- triggers validation failure
        });

        // Act & Assert -- constructor throws ArgumentException for empty queries
        var ex = Should.Throw<ArgumentException>(
            () => new SourceQueryCollection(
                sourceSettings,
                destinationSettings,
                processSettings,
                NullLogger<SourceQueryCollection>.Instance));

        ex.Message.ShouldContain("source query parameters");
    }

    [Fact]
    public void SourceQueryCollection_NullQueries_ThrowsArgumentException()
    {
        // Arrange -- null Queries triggers ArgumentException in constructor
        var sourceSettings = Options.Create(new SourceSettings
        {
            SourceConnectionString = "Server=test;Database=test;"
        });
        var destinationSettings = Options.Create(new DestinationSettings
        {
            OutputDirectory = Path.GetTempPath()
        });
        var processSettings = Options.Create(new ProcessSettings
        {
            MaxDegreeOfParallelism = 1,
            Queries = null // Null -- triggers validation failure
        });

        // Act & Assert
        var ex = Should.Throw<ArgumentException>(
            () => new SourceQueryCollection(
                sourceSettings,
                destinationSettings,
                processSettings,
                NullLogger<SourceQueryCollection>.Instance));

        ex.Message.ShouldContain("source query parameters");
    }

    [Fact]
    public void SourceQueryCollection_MissingEntityName_ThrowsArgumentException()
    {
        // Arrange -- entity with no name triggers ArgumentException
        var sourceSettings = Options.Create(new SourceSettings
        {
            SourceConnectionString = "Server=test;Database=test;"
        });
        var destinationSettings = Options.Create(new DestinationSettings
        {
            OutputDirectory = Path.GetTempPath()
        });
        var processSettings = Options.Create(new ProcessSettings
        {
            MaxDegreeOfParallelism = 1,
            Queries = new List<QuerySettings>
            {
                new() { EntityName = "" } // Empty name -- triggers validation failure
            }
        });

        // Act & Assert
        var ex = Should.Throw<ArgumentException>(
            () => new SourceQueryCollection(
                sourceSettings,
                destinationSettings,
                processSettings,
                NullLogger<SourceQueryCollection>.Instance));

        ex.Message.ShouldContain("Entity name not defined");
    }

    [Fact]
    public void SourceQueryCollection_MissingDefinitionDirectory_ThrowsException()
    {
        // Arrange -- entity with non-existent definition directory
        var sourceSettings = Options.Create(new SourceSettings
        {
            SourceConnectionString = "Server=test;Database=test;"
        });
        var destinationSettings = Options.Create(new DestinationSettings
        {
            OutputDirectory = Path.GetTempPath()
        });
        var processSettings = Options.Create(new ProcessSettings
        {
            DefinitionDirectory = Path.Combine(Path.GetTempPath(), "NonExistent_" + Guid.NewGuid().ToString("N")),
            MaxDegreeOfParallelism = 1,
            Queries = new List<QuerySettings>
            {
                new() { EntityName = "TestEntity" }
            }
        });

        // Act & Assert -- missing directory triggers plain Exception (not ArgumentException).
        // This verifies Program.Main must catch ALL Exception types, not just ArgumentException.
        var ex = Should.Throw<Exception>(
            () => new SourceQueryCollection(
                sourceSettings,
                destinationSettings,
                processSettings,
                NullLogger<SourceQueryCollection>.Instance));

        ex.Message.ShouldContain("definition directory does not exist");
    }

    #endregion

    #region Program.Main end-to-end exit code test

    [Fact]
    public async Task ProgramMain_ConfigurationError_ExitsWithCode2()
    {
        // Arrange -- create a temporary working directory with an appsettings.json
        // that has empty Queries, triggering SourceQueryCollection's ArgumentException.
        // Program.Main eagerly resolves SourceQueryCollection before app.RunAsync(),
        // so the exception reaches the catch block which sets Environment.ExitCode = 2.
        File.WriteAllText(Path.Combine(_tempDir, "appsettings.json"), """
            {
                "ProcessSettings": {
                    "Queries": [],
                    "MaxDegreeOfParallelism": 1,
                    "DefinitionDirectory": "."
                },
                "DestinationSettings": {
                    "OutputDirectory": "."
                },
                "SourceSettings": {
                    "SourceConnectionString": "Server=test;Database=test;"
                }
            }
            """);

        var dllPath = typeof(Program).Assembly.Location;

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{dllPath}\" export-file",
            WorkingDirectory = _tempDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Act -- use timeout to prevent test from hanging indefinitely if the
        // process gets stuck (the exact bug this story's eager-resolve fix addresses)
        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        var exited = process.WaitForExit(30000);
        if (!exited)
        {
            process.Kill();
        }
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        exited.ShouldBeTrue("Process did not exit within 30 seconds — possible hang");

        // Assert -- Program.Main's catch block should set exit code 2 per ADR-8
        process.ExitCode.ShouldBe(2,
            $"Expected exit code 2 for config error but got {process.ExitCode}.\nstdout: {stdout}\nstderr: {stderr}");
    }

    #endregion
}
