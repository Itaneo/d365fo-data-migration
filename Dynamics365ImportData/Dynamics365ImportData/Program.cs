namespace Dynamics365ImportData;

using Cocona;

using Dynamics365ImportData.DependencySorting;
using Dynamics365ImportData.Erp.DataManagementDefinitionGroups;
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
                .AddSingleton<XmlFileOutputFactory>();
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
            _ = builder.Host
                    .UseSerilog((context, services, configuration) =>
                                configuration
                                    .ReadFrom
                                    .Configuration(context.Configuration)
                                    .ReadFrom.Services(services)
                                    .Enrich.FromLogContext()
                    );
            CoconaApp app = builder.Build();

            _ = app.AddCommands<CommandHandler>();

            await app.RunAsync();
        }
        catch (Exception ex)
        {
            // Any unhandled exception during start-up will be caught and flushed
            Log.Fatal(ex, "An unhandled exception occured.");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}