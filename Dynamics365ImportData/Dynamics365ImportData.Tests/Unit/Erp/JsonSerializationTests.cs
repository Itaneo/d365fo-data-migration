namespace Dynamics365ImportData.Tests.Unit.Erp;

using Dynamics365ImportData.Erp.DataManagementDefinitionGroups;

using Shouldly;

using System.Text.Json;

using Xunit;

public class JsonSerializationTests
{
    [Fact]
    public void ImportFromPackageRequest_Serializes_CorrectJsonFormat()
    {
        // Arrange
        var request = new ImportFromPackageRequest
        {
            PackageUrl = new Uri("https://example.com/package.zip"),
            DefinitionGroupId = "TestGroup",
            ExecutionId = "exec-001",
            Execute = true,
            Overwrite = false,
            LegalEntityId = "USMF"
        };

        // Act
        var json = JsonSerializer.Serialize(request);

        // Assert
        json.ShouldNotBeNullOrEmpty();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("PackageUrl").GetString().ShouldBe("https://example.com/package.zip");
        root.GetProperty("DefinitionGroupId").GetString().ShouldBe("TestGroup");
        root.GetProperty("ExecutionId").GetString().ShouldBe("exec-001");
        root.GetProperty("Execute").GetBoolean().ShouldBeTrue();
        root.GetProperty("Overwrite").GetBoolean().ShouldBeFalse();
        root.GetProperty("LegalEntityId").GetString().ShouldBe("USMF");
    }

    [Fact]
    public void ExecutionIdRequest_Serializes_CorrectJsonFormat()
    {
        // Arrange
        var request = new ExecutionIdRequest("exec-abc-123");

        // Act
        var json = JsonSerializer.Serialize(request);

        // Assert
        json.ShouldNotBeNullOrEmpty();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("ExecutionId").GetString().ShouldBe("exec-abc-123");
    }

    [Fact]
    public void ImportFromPackageRequest_DataMemberAttributes_ControlPropertyNames()
    {
        // Arrange -- verify that [DataMember] and [JsonPropertyOrder] attributes
        // are present on the class by checking serialization behavior
        var request = new ImportFromPackageRequest
        {
            PackageUrl = new Uri("https://test.com/pkg.zip"),
            DefinitionGroupId = "Group1",
            ExecutionId = "ex-1",
            Execute = true,
            Overwrite = true,
            LegalEntityId = "DAT"
        };

        // Act
        var options = new JsonSerializerOptions { WriteIndented = false };
        var json = JsonSerializer.Serialize(request, options);

        // Assert -- verify the JSON contains all expected properties
        // System.Text.Json uses property names by default (not DataMember names)
        // unless JsonSerializerOptions.PropertyNamingPolicy is set
        json.ShouldContain("PackageUrl");
        json.ShouldContain("DefinitionGroupId");
        json.ShouldContain("ExecutionId");
        json.ShouldContain("Execute");
        json.ShouldContain("Overwrite");
        json.ShouldContain("LegalEntityId");

        // Characterization: System.Text.Json ignores [DataMember(Name = "...")] attributes.
        // D365FO [DataMember] specifies camelCase names (e.g., "packageUrl"), but System.Text.Json
        // serializes with PascalCase C# property names by default. This baseline documents
        // the .NET 8 behavior -- if STJ changes DataMember handling in .NET 10, these will catch it.
        json.Contains("\"packageUrl\":", StringComparison.Ordinal).ShouldBeFalse();
        json.Contains("\"definitionGroupId\":", StringComparison.Ordinal).ShouldBeFalse();
        json.Contains("\"executionId\":", StringComparison.Ordinal).ShouldBeFalse();
        json.Contains("\"legalEntityId\":", StringComparison.Ordinal).ShouldBeFalse();

        // Characterization: [JsonPropertyOrder] controls property serialization order
        // PackageUrl(1) < DefinitionGroupId(2) < ExecutionId(3) < Execute(4) < Overwrite(5) < LegalEntityId(6)
        json.IndexOf("\"PackageUrl\"").ShouldBeLessThan(json.IndexOf("\"DefinitionGroupId\""));
        json.IndexOf("\"DefinitionGroupId\"").ShouldBeLessThan(json.IndexOf("\"ExecutionId\""));
        json.IndexOf("\"ExecutionId\"").ShouldBeLessThan(json.IndexOf("\"Execute\":"));
        json.IndexOf("\"Execute\":").ShouldBeLessThan(json.IndexOf("\"Overwrite\""));
        json.IndexOf("\"Overwrite\"").ShouldBeLessThan(json.IndexOf("\"LegalEntityId\""));

        // Verify roundtrip deserialization
        var deserialized = JsonSerializer.Deserialize<ImportFromPackageRequest>(json);
        deserialized.ShouldNotBeNull();
        deserialized.PackageUrl.ShouldBe(new Uri("https://test.com/pkg.zip"));
        deserialized.DefinitionGroupId.ShouldBe("Group1");
        deserialized.LegalEntityId.ShouldBe("DAT");
    }
}
