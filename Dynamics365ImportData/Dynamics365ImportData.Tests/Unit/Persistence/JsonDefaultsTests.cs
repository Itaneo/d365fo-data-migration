namespace Dynamics365ImportData.Tests.Unit.Persistence;

using Dynamics365ImportData.Persistence;
using Dynamics365ImportData.Persistence.Models;

using Shouldly;

using System.Text.Json;

using Xunit;

public class JsonDefaultsTests
{
    [Fact]
    public void ResultJsonOptions_CamelCaseNaming_SerializesCorrectly()
    {
        // Arrange
        var entity = new EntityResult
        {
            EntityName = "Customers",
            DefinitionGroupId = "CustImport",
            Status = EntityStatus.Success,
            RecordCount = 100,
            DurationMs = 500
        };

        // Act
        var json = JsonSerializer.Serialize(entity, JsonDefaults.ResultJsonOptions);

        // Assert
        json.ShouldContain("\"entityName\"");
        json.ShouldContain("\"definitionGroupId\"");
        json.ShouldContain("\"recordCount\"");
        json.ShouldContain("\"durationMs\"");
        json.ShouldNotContain("\"EntityName\"", Case.Sensitive);
        json.ShouldNotContain("\"DefinitionGroupId\"", Case.Sensitive);
    }

    [Fact]
    public void ResultJsonOptions_EnumAsString_SerializesCorrectly()
    {
        // Arrange
        var entity = new EntityResult
        {
            EntityName = "Customers",
            Status = EntityStatus.Failed
        };

        // Act
        var json = JsonSerializer.Serialize(entity, JsonDefaults.ResultJsonOptions);

        // Assert
        json.ShouldContain("\"failed\"");
        json.ShouldNotContain("\"1\""); // Should not be numeric
    }

    [Fact]
    public void ResultJsonOptions_NullIgnored_SerializesCorrectly()
    {
        // Arrange
        var summary = new CycleSummary
        {
            TotalEntities = 3,
            Succeeded = 2,
            Failed = 1
        };

        // Act -- serialize an object with a null property
        var testObj = new { summary = summary, nullProp = (string?)null };
        var json = JsonSerializer.Serialize(testObj, JsonDefaults.ResultJsonOptions);

        // Assert
        json.ShouldNotContain("\"nullProp\"");
    }

    [Fact]
    public void ResultJsonOptions_ErrorCategory_SerializesAsCamelCaseString()
    {
        // Arrange
        var error = new EntityError
        {
            Message = "test",
            Category = ErrorCategory.DataQuality
        };

        // Act
        var json = JsonSerializer.Serialize(error, JsonDefaults.ResultJsonOptions);

        // Assert
        json.ShouldContain("\"dataQuality\"");
    }
}
