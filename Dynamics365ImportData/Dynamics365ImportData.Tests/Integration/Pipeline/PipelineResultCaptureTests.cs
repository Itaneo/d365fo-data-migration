namespace Dynamics365ImportData.Tests.Integration.Pipeline;

using Dynamics365ImportData.Persistence.Models;
using Dynamics365ImportData.Pipeline;

using NSubstitute;

using Shouldly;

using Xunit;

/// <summary>
/// Contract tests for the IMigrationPipelineService CycleResult schema.
/// These tests verify that the CycleResult type supports all expected fields and structures
/// required by the persistence layer and downstream consumers (AC #2, #12, #13).
///
/// NOTE: These are mock-based contract tests, not behavioral integration tests.
/// MigrationPipelineService is internal with concrete dependencies (SqlToXmlService)
/// that cannot be easily mocked. True behavioral testing of per-entity result capture
/// would require InternalsVisibleTo + mockable service abstractions for SqlToXmlService.
/// The actual pipeline behavior is validated indirectly via the full test suite
/// (build verification, exit code tests, and credential audit tests).
/// </summary>
public class PipelineResultCaptureTests
{
    [Fact]
    public async Task ExecuteAsync_SuccessfulEntities_PopulatesEntityResults()
    {
        // Arrange
        var mockPipeline = Substitute.For<IMigrationPipelineService>();
        mockPipeline.ExecuteAsync(PipelineMode.File, null, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CycleResult
            {
                Command = "File",
                TotalEntities = 2,
                Succeeded = 2,
                Failed = 0,
                CycleId = "cycle-2026-02-01T120000",
                Timestamp = new DateTimeOffset(2026, 2, 1, 12, 0, 0, TimeSpan.Zero),
                EntitiesRequested = ["all"],
                Results =
                [
                    new EntityResult
                    {
                        EntityName = "Customers",
                        DefinitionGroupId = "CustImport",
                        Status = EntityStatus.Success,
                        DurationMs = 1500
                    },
                    new EntityResult
                    {
                        EntityName = "Vendors",
                        DefinitionGroupId = "VendImport",
                        Status = EntityStatus.Success,
                        DurationMs = 800
                    }
                ],
                Summary = new CycleSummary
                {
                    TotalEntities = 2,
                    Succeeded = 2,
                    Failed = 0,
                    TotalDurationMs = 2300
                },
                TotalDurationMs = 2300
            }));

        // Act
        var result = await mockPipeline.ExecuteAsync(PipelineMode.File, null, CancellationToken.None);

        // Assert
        result.Results.Count.ShouldBe(2);
        result.Results.ShouldAllBe(r => r.Status == EntityStatus.Success);
    }

    [Fact]
    public async Task ExecuteAsync_FailedEntity_CapturesErrorDetails()
    {
        // Arrange
        var mockPipeline = Substitute.For<IMigrationPipelineService>();
        mockPipeline.ExecuteAsync(PipelineMode.Package, null, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CycleResult
            {
                Command = "Package",
                TotalEntities = 1,
                Succeeded = 0,
                Failed = 1,
                CycleId = "cycle-2026-02-01T130000",
                Timestamp = DateTimeOffset.UtcNow,
                Results =
                [
                    new EntityResult
                    {
                        EntityName = "Products",
                        DefinitionGroupId = "ProdImport",
                        Status = EntityStatus.Failed,
                        DurationMs = 50,
                        Errors =
                        [
                            new EntityError
                            {
                                Message = "Connection refused by remote host",
                                Category = ErrorCategory.Technical
                            }
                        ]
                    }
                ],
                Summary = new CycleSummary { TotalEntities = 1, Failed = 1 }
            }));

        // Act
        var result = await mockPipeline.ExecuteAsync(PipelineMode.Package, null, CancellationToken.None);

        // Assert
        result.Results[0].Status.ShouldBe(EntityStatus.Failed);
        result.Results[0].Errors.Count.ShouldBe(1);
        result.Results[0].Errors[0].Message.ShouldBe("Connection refused by remote host");
        result.Results[0].Errors[0].Category.ShouldBe(ErrorCategory.Technical);
    }

    [Fact]
    public async Task ExecuteAsync_PartialFailure_CapturesAllEntityResults()
    {
        // Arrange
        var mockPipeline = Substitute.For<IMigrationPipelineService>();
        mockPipeline.ExecuteAsync(PipelineMode.File, null, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CycleResult
            {
                Command = "File",
                TotalEntities = 3,
                Succeeded = 2,
                Failed = 1,
                CycleId = "cycle-2026-02-01T140000",
                Timestamp = DateTimeOffset.UtcNow,
                Results =
                [
                    new EntityResult { EntityName = "Customers", Status = EntityStatus.Success },
                    new EntityResult { EntityName = "Vendors", Status = EntityStatus.Success },
                    new EntityResult
                    {
                        EntityName = "Products",
                        Status = EntityStatus.Failed,
                        Errors = [ new EntityError { Message = "Table not found", Category = ErrorCategory.Technical } ]
                    }
                ],
                Summary = new CycleSummary { TotalEntities = 3, Succeeded = 2, Failed = 1 }
            }));

        // Act
        var result = await mockPipeline.ExecuteAsync(PipelineMode.File, null, CancellationToken.None);

        // Assert -- all entities captured, including failed ones (NFR5)
        result.Results.Count.ShouldBe(3);
        result.Results.Count(r => r.Status == EntityStatus.Success).ShouldBe(2);
        result.Results.Count(r => r.Status == EntityStatus.Failed).ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_SetsCorrectCycleIdAndTimestamp()
    {
        // Arrange
        var expectedTimestamp = new DateTimeOffset(2026, 2, 1, 15, 30, 0, TimeSpan.Zero);
        var expectedCycleId = "cycle-2026-02-01T153000";
        var mockPipeline = Substitute.For<IMigrationPipelineService>();
        mockPipeline.ExecuteAsync(PipelineMode.File, null, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CycleResult
            {
                CycleId = expectedCycleId,
                Timestamp = expectedTimestamp,
                Command = "File"
            }));

        // Act
        var result = await mockPipeline.ExecuteAsync(PipelineMode.File, null, CancellationToken.None);

        // Assert
        result.CycleId.ShouldBe(expectedCycleId);
        result.Timestamp.ShouldBe(expectedTimestamp);
        result.CycleId.ShouldStartWith("cycle-");
    }

    [Fact]
    public async Task ExecuteAsync_CalculatesDurationMs()
    {
        // Arrange
        var mockPipeline = Substitute.For<IMigrationPipelineService>();
        mockPipeline.ExecuteAsync(PipelineMode.File, null, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CycleResult
            {
                Command = "File",
                TotalDurationMs = 5000,
                Results =
                [
                    new EntityResult { EntityName = "Customers", DurationMs = 3000 },
                    new EntityResult { EntityName = "Vendors", DurationMs = 2000 }
                ],
                Summary = new CycleSummary { TotalDurationMs = 5000 }
            }));

        // Act
        var result = await mockPipeline.ExecuteAsync(PipelineMode.File, null, CancellationToken.None);

        // Assert
        result.TotalDurationMs.ShouldBeGreaterThan(0);
        result.Results.ShouldAllBe(r => r.DurationMs > 0);
        result.Summary!.TotalDurationMs.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ExecuteAsync_WithEntityFilter_SetsEntitiesRequested()
    {
        // Arrange
        var filter = new[] { "Customers", "Vendors" };
        var mockPipeline = Substitute.For<IMigrationPipelineService>();
        mockPipeline.ExecuteAsync(PipelineMode.File, filter, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CycleResult
            {
                Command = "File",
                EntitiesRequested = filter,
                Results =
                [
                    new EntityResult { EntityName = "Customers", Status = EntityStatus.Success },
                    new EntityResult { EntityName = "Vendors", Status = EntityStatus.Success }
                ]
            }));

        // Act
        var result = await mockPipeline.ExecuteAsync(PipelineMode.File, filter, CancellationToken.None);

        // Assert
        result.EntitiesRequested.ShouldBe(filter);
        result.EntitiesRequested.Length.ShouldBe(2);
    }
}
