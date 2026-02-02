namespace Dynamics365ImportData;

using Cocona;

using Dynamics365ImportData.DependencySorting;
using Dynamics365ImportData.Erp.DataManagementDefinitionGroups;
using Dynamics365ImportData.Comparison;
using Dynamics365ImportData.Reporting;
using Dynamics365ImportData.Fingerprinting;
using Dynamics365ImportData.Persistence;
using Dynamics365ImportData.Pipeline;
using Dynamics365ImportData.Sanitization;
using Dynamics365ImportData.Services;
using Dynamics365ImportData.Settings;
using Dynamics365ImportData.XmlOutput;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Serilog;
using Serilog.Events;

using System;
using System.Threading.Tasks;

public class Program
{
    private static void AddConfiguration(IConfigurationBuilder configuration, string[] args)
    {
        _ = configuration
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", false)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", true)
            .AddUserSecrets<Program>()
            .AddEnvironmentVariables()
            .AddCommandLine(args);
    }

    private static void InitializeSerilog()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Override(nameof(Microsoft), LogEventLevel.Warning)
            .MinimumLevel.Override(nameof(System), LogEventLevel.Error)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateBootstrapLogger();
    }

    private static async Task Main(string[] args)
    {
        InitializeSerilog();
        try
        {
            Cocona.Builder.CoconaAppBuilder builder = CoconaApp.CreateBuilder();

            AddConfiguration(builder.Configuration, args);

            IServiceCollection services = builder.Services
                .AddLogging(log => _ = log.AddSerilog())
            .AddTransient<SqlToXmlService>()
                .AddSingleton<SourceQueryCollection>()
                .AddSingleton<XmlD365FnoOutputFactory>()
                .AddSingleton<XmlPackageFileOutputFactory>()
                .AddSingleton<XmlFileOutputFactory>()
                .AddTransient<IMigrationPipelineService, MigrationPipelineService>();

            // Result Persistence
            _ = services.AddSingleton<IMigrationResultRepository, JsonFileMigrationResultRepository>();
            _ = services.AddSingleton<IResultSanitizer, RegexResultSanitizer>();

            // Error Analysis
            _ = services.AddSingleton<IErrorFingerprinter, ErrorFingerprinter>();
            _ = services.AddTransient<IErrorComparisonService, ErrorComparisonService>();

            // Reporting
            _ = services.AddTransient<IErrorComparisonReportService, ErrorComparisonReportService>();
            _ = services.AddTransient<IReadinessReportService, ReadinessReportService>();
            _ = services.AddHttpClient<IDynamics365FinanceDataManagementGroups, Dynamics365FinanceDataManagementGroups>(
                    (services, httpClient) => httpClient.Timeout = new TimeSpan(0,
                                                                                services.GetRequiredService<IOptions<Dynamics365Settings>>().Value.ImportTimeout,
                                                                                0));
            _ = services.AddOptions<SourceSettings>()
                .Bind(builder.Configuration.GetSection(nameof(SourceSettings)));
            _ = services.AddOptions<DestinationSettings>()
                .Bind(builder.Configuration.GetSection(nameof(DestinationSettings)));
            _ = services.AddOptions<ProcessSettings>()
                .Bind(builder.Configuration.GetSection(nameof(ProcessSettings)));
            _ = services.AddOptions<Dynamics365Settings>()
                .Bind(builder.Configuration.GetSection(nameof(Dynamics365Settings)));
            _ = services.AddOptions<PersistenceSettings>()
                .Bind(builder.Configuration.GetSection(nameof(PersistenceSettings)));
            _ = services.AddOptions<ReportSettings>()
                .Bind(builder.Configuration.GetSection(nameof(ReportSettings)));
            _ = builder.Host
                    .UseSerilog((context, services, configuration) =>
                                configuration
                                    .ReadFrom
                                    .Configuration(context.Configuration)
                                    .ReadFrom.Services(services)
                                    .Enrich.FromLogContext()
                    );
            CoconaApp app = builder.Build();

            // Eagerly resolve SourceQueryCollection to validate configuration before
            // Cocona starts command routing. Without this, SourceQueryCollection's
            // constructor exception occurs during DI resolution within Cocona's
            // command invoker, where the Generic Host intercepts it and hangs
            // instead of letting it propagate to this catch block.
            _ = app.Services.GetRequiredService<SourceQueryCollection>();

            _ = app.AddCommands<CommandHandler>();

            await app.RunAsync();
        }
        catch (Exception ex)
        {
            // Any exception reaching here is a pre-command failure (config, startup, routing).
            // CommandHandler catches ALL exceptions within command methods, so exceptions
            // that escape app.RunAsync() are inherently configuration/startup errors.
            // SourceQueryCollection throws ArgumentException (3 cases) and plain Exception
            // (7 cases) for config validation -- all must produce exit code 2 per ADR-8.
            // Note: rare runtime failures (OutOfMemoryException, TypeLoadException) also
            // map to exit code 2 here. This is an accepted trade-off for simplicity --
            // these are not config errors but are indistinguishable at this level.
            Log.Fatal(ex, "Configuration error");
            Environment.ExitCode = 2;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}