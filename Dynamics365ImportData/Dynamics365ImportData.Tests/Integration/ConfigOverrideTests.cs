namespace Dynamics365ImportData.Tests.Integration;

using Dynamics365ImportData.Settings;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

/// <summary>
/// Integration tests for CLI config override precedence (FR19, FR20).
/// Verifies that .AddCommandLine(args) at the end of the configuration pipeline
/// takes precedence over appsettings.json values, and that the standard .NET
/// configuration key format (--SectionName:PropertyName value) works correctly.
///
/// NOTE: These tests verify the .NET configuration pipeline behavior (AddCommandLine
/// overrides AddInMemoryCollection). They do NOT test Cocona's runtime argument parsing
/// compatibility with --Key:SubKey format. Cocona compatibility with config override
/// args requires manual end-to-end verification since Cocona parses the same args
/// array for its own command/option matching.
/// </summary>
public class ConfigOverrideTests
{
    /// <summary>
    /// Builds a configuration pipeline that mirrors Program.AddConfiguration():
    /// appsettings.json (in-memory) → AddCommandLine(args) to verify precedence.
    /// </summary>
    private static IConfiguration BuildConfiguration(
        Dictionary<string, string?> baseConfig,
        string[] commandLineArgs)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(baseConfig)
            .AddCommandLine(commandLineArgs)
            .Build();
    }

    [Fact]
    public void ConfigOverride_OutputDirectory_CLITakesPrecedence()
    {
        // Arrange -- base config has one value, CLI overrides it
        var baseConfig = new Dictionary<string, string?>
        {
            ["DestinationSettings:OutputDirectory"] = @"C:\original-output"
        };
        var cliArgs = new[] { "--DestinationSettings:OutputDirectory", @"C:\override-output" };

        // Act
        var config = BuildConfiguration(baseConfig, cliArgs);
        var services = new ServiceCollection();
        services.Configure<DestinationSettings>(config.GetSection(nameof(DestinationSettings)));
        var provider = services.BuildServiceProvider();
        var settings = provider.GetRequiredService<IOptions<DestinationSettings>>().Value;

        // Assert -- CLI value should win
        settings.OutputDirectory.ShouldBe(@"C:\override-output");
    }

    [Fact]
    public void ConfigOverride_ImportTimeout_CLITakesPrecedence()
    {
        // Arrange -- base config has timeout 60, CLI overrides to 120
        var baseConfig = new Dictionary<string, string?>
        {
            ["Dynamics365Settings:ImportTimeout"] = "60"
        };
        var cliArgs = new[] { "--Dynamics365Settings:ImportTimeout", "120" };

        // Act
        var config = BuildConfiguration(baseConfig, cliArgs);
        var services = new ServiceCollection();
        services.Configure<Dynamics365Settings>(config.GetSection(nameof(Dynamics365Settings)));
        var provider = services.BuildServiceProvider();
        var settings = provider.GetRequiredService<IOptions<Dynamics365Settings>>().Value;

        // Assert -- CLI value should win
        settings.ImportTimeout.ShouldBe(120);
    }

    [Fact]
    public void ConfigOverride_NoCLIArgs_UsesBaseValues()
    {
        // Arrange -- base config only, no CLI override (backward compatibility)
        var baseConfig = new Dictionary<string, string?>
        {
            ["DestinationSettings:OutputDirectory"] = @"C:\original-output",
            ["Dynamics365Settings:ImportTimeout"] = "60"
        };
        var cliArgs = Array.Empty<string>();

        // Act
        var config = BuildConfiguration(baseConfig, cliArgs);
        var services = new ServiceCollection();
        services.Configure<DestinationSettings>(config.GetSection(nameof(DestinationSettings)));
        services.Configure<Dynamics365Settings>(config.GetSection(nameof(Dynamics365Settings)));
        var provider = services.BuildServiceProvider();
        var destSettings = provider.GetRequiredService<IOptions<DestinationSettings>>().Value;
        var d365Settings = provider.GetRequiredService<IOptions<Dynamics365Settings>>().Value;

        // Assert -- base values should be used
        destSettings.OutputDirectory.ShouldBe(@"C:\original-output");
        d365Settings.ImportTimeout.ShouldBe(60);
    }

    [Fact]
    public void ConfigOverride_MaxDegreeOfParallelism_CLITakesPrecedence()
    {
        // Arrange
        var baseConfig = new Dictionary<string, string?>
        {
            ["ProcessSettings:MaxDegreeOfParallelism"] = "4"
        };
        var cliArgs = new[] { "--ProcessSettings:MaxDegreeOfParallelism", "8" };

        // Act
        var config = BuildConfiguration(baseConfig, cliArgs);
        var services = new ServiceCollection();
        services.Configure<ProcessSettings>(config.GetSection(nameof(ProcessSettings)));
        var provider = services.BuildServiceProvider();
        var settings = provider.GetRequiredService<IOptions<ProcessSettings>>().Value;

        // Assert
        settings.MaxDegreeOfParallelism.ShouldBe(8);
    }

    [Fact]
    public void ConfigOverride_MultipleOverrides_AllApplied()
    {
        // Arrange -- override multiple settings simultaneously
        var baseConfig = new Dictionary<string, string?>
        {
            ["DestinationSettings:OutputDirectory"] = @"C:\original",
            ["Dynamics365Settings:ImportTimeout"] = "60",
            ["ProcessSettings:MaxDegreeOfParallelism"] = "4"
        };
        var cliArgs = new[]
        {
            "--DestinationSettings:OutputDirectory", @"C:\new",
            "--Dynamics365Settings:ImportTimeout", "120",
            "--ProcessSettings:MaxDegreeOfParallelism", "8"
        };

        // Act
        var config = BuildConfiguration(baseConfig, cliArgs);
        var services = new ServiceCollection();
        services.Configure<DestinationSettings>(config.GetSection(nameof(DestinationSettings)));
        services.Configure<Dynamics365Settings>(config.GetSection(nameof(Dynamics365Settings)));
        services.Configure<ProcessSettings>(config.GetSection(nameof(ProcessSettings)));
        var provider = services.BuildServiceProvider();

        var destSettings = provider.GetRequiredService<IOptions<DestinationSettings>>().Value;
        var d365Settings = provider.GetRequiredService<IOptions<Dynamics365Settings>>().Value;
        var processSettings = provider.GetRequiredService<IOptions<ProcessSettings>>().Value;

        // Assert -- all CLI overrides should take effect
        destSettings.OutputDirectory.ShouldBe(@"C:\new");
        d365Settings.ImportTimeout.ShouldBe(120);
        processSettings.MaxDegreeOfParallelism.ShouldBe(8);
    }

    [Fact]
    public void ConfigOverride_PartialOverride_MixesBaseAndCLI()
    {
        // Arrange -- override only one setting, keep others from base
        var baseConfig = new Dictionary<string, string?>
        {
            ["DestinationSettings:OutputDirectory"] = @"C:\original",
            ["Dynamics365Settings:ImportTimeout"] = "60"
        };
        var cliArgs = new[] { "--Dynamics365Settings:ImportTimeout", "120" };

        // Act
        var config = BuildConfiguration(baseConfig, cliArgs);
        var services = new ServiceCollection();
        services.Configure<DestinationSettings>(config.GetSection(nameof(DestinationSettings)));
        services.Configure<Dynamics365Settings>(config.GetSection(nameof(Dynamics365Settings)));
        var provider = services.BuildServiceProvider();

        var destSettings = provider.GetRequiredService<IOptions<DestinationSettings>>().Value;
        var d365Settings = provider.GetRequiredService<IOptions<Dynamics365Settings>>().Value;

        // Assert -- only the overridden setting changes
        destSettings.OutputDirectory.ShouldBe(@"C:\original"); // Not overridden
        d365Settings.ImportTimeout.ShouldBe(120); // Overridden
    }
}
