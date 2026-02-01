namespace Dynamics365ImportData.Tests.Unit.Settings;

using Dynamics365ImportData.Settings;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

public class ConfigurationBindingTests
{
    [Fact]
    public void Dynamics365Settings_BindsFromConfiguration_AllPropertiesSet()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            ["Dynamics365:ClientId"] = "test-client-id",
            ["Dynamics365:ImportTimeout"] = "120",
            ["Dynamics365:LegalEntityId"] = "USMF",
            ["Dynamics365:Secret"] = "test-secret",
            ["Dynamics365:Tenant"] = "test-tenant.onmicrosoft.com",
            ["Dynamics365:Url"] = "https://test.dynamics.com"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.Configure<Dynamics365Settings>(configuration.GetSection("Dynamics365"));
        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<Dynamics365Settings>>();
        var settings = options.Value;

        // Assert
        settings.ClientId.ShouldBe("test-client-id");
        settings.ImportTimeout.ShouldBe(120);
        settings.LegalEntityId.ShouldBe("USMF");
        settings.Secret.ShouldBe("test-secret");
        settings.Tenant.ShouldBe("test-tenant.onmicrosoft.com");
        settings.Url.ShouldBe(new Uri("https://test.dynamics.com"));
    }

    [Fact]
    public void ProcessSettings_BindsFromConfiguration_QueriesPopulated()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            ["Process:DefinitionDirectory"] = "/definitions",
            ["Process:MaxDegreeOfParallelism"] = "4",
            ["Process:Queries:0:EntityName"] = "Customer",
            ["Process:Queries:0:DefinitionGroupId"] = "CustGroup",
            ["Process:Queries:0:ManifestFileName"] = "Manifest.xml",
            ["Process:Queries:0:PackageHeaderFileName"] = "PackageHeader.xml",
            ["Process:Queries:0:QueryFileName"] = "customer.sql",
            ["Process:Queries:0:RecordsPerFile"] = "5000",
            ["Process:Queries:1:EntityName"] = "Vendor",
            ["Process:Queries:1:DefinitionGroupId"] = "VendGroup",
            ["Process:Queries:1:RecordsPerFile"] = "3000",
            ["Process:Queries:1:Dependencies:0"] = "Customer"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.Configure<ProcessSettings>(configuration.GetSection("Process"));
        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<ProcessSettings>>();
        var settings = options.Value;

        // Assert
        settings.DefinitionDirectory.ShouldBe("/definitions");
        settings.MaxDegreeOfParallelism.ShouldBe(4);
        settings.Queries.ShouldNotBeNull();
        settings.Queries!.Count.ShouldBe(2);

        settings.Queries[0].EntityName.ShouldBe("Customer");
        settings.Queries[0].DefinitionGroupId.ShouldBe("CustGroup");
        settings.Queries[0].ManifestFileName.ShouldBe("Manifest.xml");
        settings.Queries[0].PackageHeaderFileName.ShouldBe("PackageHeader.xml");
        settings.Queries[0].QueryFileName.ShouldBe("customer.sql");
        settings.Queries[0].RecordsPerFile.ShouldBe(5000);

        settings.Queries[1].EntityName.ShouldBe("Vendor");
        settings.Queries[1].DefinitionGroupId.ShouldBe("VendGroup");
        settings.Queries[1].RecordsPerFile.ShouldBe(3000);
        var vendorDeps = settings.Queries[1].Dependencies;
        vendorDeps.ShouldNotBeNull();
        vendorDeps!.Count.ShouldBe(1);
        vendorDeps[0].ShouldBe("Customer");
    }

    [Fact]
    public void Dynamics365Settings_Defaults_ImportTimeoutIs60()
    {
        // Arrange -- empty configuration, relying on class defaults
        var configData = new Dictionary<string, string?>();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.Configure<Dynamics365Settings>(configuration.GetSection("Dynamics365"));
        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<Dynamics365Settings>>();
        var settings = options.Value;

        // Assert
        settings.ImportTimeout.ShouldBe(60);
        settings.ClientId.ShouldBeNull();
        settings.Secret.ShouldBeNull();
        settings.Tenant.ShouldBeNull();
        settings.LegalEntityId.ShouldBeNull();
        settings.Url.ShouldBeNull();
    }
}
