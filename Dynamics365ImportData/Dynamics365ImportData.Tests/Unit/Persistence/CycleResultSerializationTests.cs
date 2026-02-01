namespace Dynamics365ImportData.Tests.Unit.Persistence;

using Dynamics365ImportData.Persistence;
using Dynamics365ImportData.Persistence.Models;
using Dynamics365ImportData.Pipeline;

using Shouldly;

using System.Text.Json;

using Xunit;

public class CycleResultSerializationTests
{
    [Fact]
    public void CycleResult_RoundTrip_PreservesAllFields()
    {
        // Arrange
        var original = new CycleResult
        {
            Command = "File",
            TotalEntities = 3,
            Succeeded = 2,
            Failed = 1,
            CycleId = "cycle-2026-02-01T120000",
            Timestamp = new DateTimeOffset(2026, 2, 1, 12, 0, 0, TimeSpan.Zero),
            EntitiesRequested = ["Customers", "Vendors", "Products"],
            Results =
            [
                new EntityResult
                {
                    EntityName = "Customers",
                    DefinitionGroupId = "CustImport",
                    Status = EntityStatus.Success,
                    RecordCount = 0,
                    DurationMs = 1500
                },
                new EntityResult
                {
                    EntityName = "Vendors",
                    DefinitionGroupId = "VendImport",
                    Status = EntityStatus.Success,
                    RecordCount = 0,
                    DurationMs = 800
                },
                new EntityResult
                {
                    EntityName = "Products",
                    DefinitionGroupId = "ProdImport",
                    Status = EntityStatus.Failed,
                    RecordCount = 0,
                    DurationMs = 100,
                    Errors =
                    [
                        new EntityError
                        {
                            Message = "Connection refused",
                            Fingerprint = "",
                            Category = ErrorCategory.Technical
                        }
                    ]
                }
            ],
            Summary = new CycleSummary
            {
                TotalEntities = 3,
                Succeeded = 2,
                Failed = 1,
                Warnings = 0,
                Skipped = 0,
                TotalDurationMs = 2400
            },
            TotalDurationMs = 2400
        };

        // Act
        var json = JsonSerializer.Serialize(original, JsonDefaults.ResultJsonOptions);
        var deserialized = JsonSerializer.Deserialize<CycleResult>(json, JsonDefaults.ResultJsonOptions);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.Command.ShouldBe(original.Command);
        deserialized.TotalEntities.ShouldBe(original.TotalEntities);
        deserialized.Succeeded.ShouldBe(original.Succeeded);
        deserialized.Failed.ShouldBe(original.Failed);
        deserialized.CycleId.ShouldBe(original.CycleId);
        deserialized.Timestamp.ShouldBe(original.Timestamp);
        deserialized.EntitiesRequested.ShouldBe(original.EntitiesRequested);
        deserialized.Results.Count.ShouldBe(3);
        deserialized.Results[2].Errors.Count.ShouldBe(1);
        deserialized.Results[2].Errors[0].Message.ShouldBe("Connection refused");
        deserialized.Results[2].Errors[0].Category.ShouldBe(ErrorCategory.Technical);
        deserialized.Summary.ShouldNotBeNull();
        deserialized.Summary!.TotalEntities.ShouldBe(3);
        deserialized.Summary.Succeeded.ShouldBe(2);
        deserialized.Summary.Failed.ShouldBe(1);
        deserialized.TotalDurationMs.ShouldBe(2400);
    }

    [Fact]
    public void CycleResult_WithErrors_SerializesCorrectly()
    {
        // Arrange
        var result = new CycleResult
        {
            CycleId = "cycle-2026-02-01T120000",
            Timestamp = DateTimeOffset.UtcNow,
            Command = "Package",
            Results =
            [
                new EntityResult
                {
                    EntityName = "Customers",
                    Status = EntityStatus.Failed,
                    Errors =
                    [
                        new EntityError
                        {
                            Message = "Timeout waiting for response",
                            Category = ErrorCategory.Technical
                        },
                        new EntityError
                        {
                            Message = "Missing required field: CustomerGroup",
                            Category = ErrorCategory.DataQuality
                        }
                    ]
                }
            ]
        };

        // Act
        var json = JsonSerializer.Serialize(result, JsonDefaults.ResultJsonOptions);

        // Assert
        json.ShouldContain("\"errors\"");
        json.ShouldContain("Timeout waiting for response");
        json.ShouldContain("Missing required field: CustomerGroup");
        json.ShouldContain("\"technical\"");
        json.ShouldContain("\"dataQuality\"");
    }

    [Fact]
    public void CycleResult_JsonPropertyNaming_MatchesAdr1Schema()
    {
        // Arrange
        var result = new CycleResult
        {
            CycleId = "cycle-2026-02-01T120000",
            Timestamp = DateTimeOffset.UtcNow,
            Command = "File",
            EntitiesRequested = ["all"],
            Results = [],
            Summary = new CycleSummary { TotalEntities = 0 },
            TotalDurationMs = 100
        };

        // Act
        var json = JsonSerializer.Serialize(result, JsonDefaults.ResultJsonOptions);

        // Assert -- verify camelCase property names matching ADR-1 schema
        json.ShouldContain("\"cycleId\"");
        json.ShouldContain("\"timestamp\"");
        json.ShouldContain("\"command\"");
        json.ShouldContain("\"entitiesRequested\"");
        json.ShouldContain("\"results\"");
        json.ShouldContain("\"summary\"");
        json.ShouldContain("\"totalDurationMs\"");
    }
}
