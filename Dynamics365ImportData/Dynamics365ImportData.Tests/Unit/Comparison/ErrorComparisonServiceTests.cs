namespace Dynamics365ImportData.Tests.Unit.Comparison;

using Dynamics365ImportData.Comparison;
using Dynamics365ImportData.Comparison.Models;
using Dynamics365ImportData.Persistence;
using Dynamics365ImportData.Persistence.Models;
using Dynamics365ImportData.Pipeline;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

using Xunit;

public class ErrorComparisonServiceTests
{
    private readonly IMigrationResultRepository _repository;
    private readonly ErrorComparisonService _service;

    public ErrorComparisonServiceTests()
    {
        _repository = Substitute.For<IMigrationResultRepository>();
        _service = new ErrorComparisonService(
            _repository,
            NullLogger<ErrorComparisonService>.Instance);
    }

    private static CycleResult CreateCycleResult(string cycleId, params EntityResult[] results)
    {
        return new CycleResult
        {
            CycleId = cycleId,
            Timestamp = DateTimeOffset.UtcNow,
            Results = results.ToList()
        };
    }

    private static EntityResult CreateEntityResult(string entityName, EntityStatus status, params EntityError[] errors)
    {
        return new EntityResult
        {
            EntityName = entityName,
            Status = status,
            Errors = errors.ToList()
        };
    }

    private static EntityError CreateError(string message, string fingerprint, ErrorCategory category = ErrorCategory.Technical)
    {
        return new EntityError
        {
            Message = message,
            Fingerprint = fingerprint,
            Category = category
        };
    }

    [Fact]
    public async Task CompareAsync_TwoCycles_ClassifiesNewErrors()
    {
        // Arrange
        var previous = CreateCycleResult("cycle-prev",
            CreateEntityResult("Customers", EntityStatus.Failed,
                CreateError("Old error", "aabb000000000001")));
        var current = CreateCycleResult("cycle-curr",
            CreateEntityResult("Customers", EntityStatus.Failed,
                CreateError("Brand new error", "ccdd000000000002")));

        _repository.GetLatestCycleResultsAsync(2, Arg.Any<CancellationToken>())
            .Returns(new List<CycleResult> { current, previous });

        // Act
        var result = await _service.CompareAsync();

        // Assert
        result.IsFirstCycle.ShouldBeFalse();
        result.EntityComparisons.Count.ShouldBe(1);
        result.EntityComparisons[0].NewErrors.Count.ShouldBe(1);
        result.EntityComparisons[0].NewErrors[0].Fingerprint.ShouldBe("ccdd000000000002");
        result.EntityComparisons[0].NewErrors[0].Classification.ShouldBe(ErrorClassification.New);
    }

    [Fact]
    public async Task CompareAsync_TwoCycles_ClassifiesCarryOverErrors()
    {
        // Arrange
        var sharedFingerprint = "aabb000000000001";
        var previous = CreateCycleResult("cycle-prev",
            CreateEntityResult("Customers", EntityStatus.Failed,
                CreateError("Recurring error", sharedFingerprint)));
        var current = CreateCycleResult("cycle-curr",
            CreateEntityResult("Customers", EntityStatus.Failed,
                CreateError("Recurring error", sharedFingerprint)));

        _repository.GetLatestCycleResultsAsync(2, Arg.Any<CancellationToken>())
            .Returns(new List<CycleResult> { current, previous });

        // Act
        var result = await _service.CompareAsync();

        // Assert
        result.EntityComparisons[0].CarryOverErrors.Count.ShouldBe(1);
        result.EntityComparisons[0].CarryOverErrors[0].Classification.ShouldBe(ErrorClassification.CarryOver);
    }

    [Fact]
    public async Task CompareAsync_TwoCycles_IdentifiesResolvedErrors()
    {
        // Arrange
        var previous = CreateCycleResult("cycle-prev",
            CreateEntityResult("Customers", EntityStatus.Failed,
                CreateError("Fixed error", "aabb000000000001")));
        var current = CreateCycleResult("cycle-curr",
            CreateEntityResult("Customers", EntityStatus.Success));

        _repository.GetLatestCycleResultsAsync(2, Arg.Any<CancellationToken>())
            .Returns(new List<CycleResult> { current, previous });

        // Act
        var result = await _service.CompareAsync();

        // Assert
        result.EntityComparisons.Count.ShouldBe(1);
        result.EntityComparisons[0].ResolvedFingerprints.Count.ShouldBe(1);
        result.EntityComparisons[0].ResolvedFingerprints[0].ShouldBe("aabb000000000001");
    }

    [Fact]
    public async Task CompareAsync_FirstCycle_ReturnsIsFirstCycleTrue()
    {
        // Arrange -- only one cycle exists
        var current = CreateCycleResult("cycle-curr",
            CreateEntityResult("Customers", EntityStatus.Success));

        _repository.GetLatestCycleResultsAsync(2, Arg.Any<CancellationToken>())
            .Returns(new List<CycleResult> { current });

        // Act
        var result = await _service.CompareAsync();

        // Assert
        result.IsFirstCycle.ShouldBeTrue();
        result.CurrentCycleId.ShouldBe("cycle-curr");
        result.EntityComparisons.ShouldBeEmpty();
    }

    [Fact]
    public async Task CompareAsync_NoCycles_ReturnsIsFirstCycleTrue()
    {
        // Arrange -- zero cycles exist
        _repository.GetLatestCycleResultsAsync(2, Arg.Any<CancellationToken>())
            .Returns(new List<CycleResult>());

        // Act
        var result = await _service.CompareAsync();

        // Assert
        result.IsFirstCycle.ShouldBeTrue();
        result.EntityComparisons.ShouldBeEmpty();
    }

    [Fact]
    public async Task CompareAsync_EntityInCurrentNotInPrevious_AllErrorsNew()
    {
        // Arrange
        var previous = CreateCycleResult("cycle-prev",
            CreateEntityResult("Vendors", EntityStatus.Success));
        var current = CreateCycleResult("cycle-curr",
            CreateEntityResult("Customers", EntityStatus.Failed,
                CreateError("Error A", "aaaa000000000001"),
                CreateError("Error B", "bbbb000000000002")));

        _repository.GetLatestCycleResultsAsync(2, Arg.Any<CancellationToken>())
            .Returns(new List<CycleResult> { current, previous });

        // Act
        var result = await _service.CompareAsync();

        // Assert
        result.EntityComparisons.ShouldContain(c => c.EntityName == "Customers");
        var customerComparison = result.EntityComparisons.First(c => c.EntityName == "Customers");
        customerComparison.NewErrors.Count.ShouldBe(2);
        customerComparison.CarryOverErrors.ShouldBeEmpty();
    }

    [Fact]
    public async Task CompareAsync_EntityInPreviousNotInCurrent_AllErrorsResolved()
    {
        // Arrange
        var previous = CreateCycleResult("cycle-prev",
            CreateEntityResult("Customers", EntityStatus.Failed,
                CreateError("Old error A", "aaaa000000000001"),
                CreateError("Old error B", "bbbb000000000002")));
        var current = CreateCycleResult("cycle-curr",
            CreateEntityResult("Vendors", EntityStatus.Success));

        _repository.GetLatestCycleResultsAsync(2, Arg.Any<CancellationToken>())
            .Returns(new List<CycleResult> { current, previous });

        // Act
        var result = await _service.CompareAsync();

        // Assert
        result.EntityComparisons.ShouldContain(c => c.EntityName == "Customers");
        var customerComparison = result.EntityComparisons.First(c => c.EntityName == "Customers");
        customerComparison.ResolvedFingerprints.Count.ShouldBe(2);
        customerComparison.NewErrors.ShouldBeEmpty();
        customerComparison.CarryOverErrors.ShouldBeEmpty();
    }

    [Fact]
    public async Task CompareAsync_BothCyclesNoErrors_EmptyComparisons()
    {
        // Arrange
        var previous = CreateCycleResult("cycle-prev",
            CreateEntityResult("Customers", EntityStatus.Success));
        var current = CreateCycleResult("cycle-curr",
            CreateEntityResult("Customers", EntityStatus.Success));

        _repository.GetLatestCycleResultsAsync(2, Arg.Any<CancellationToken>())
            .Returns(new List<CycleResult> { current, previous });

        // Act
        var result = await _service.CompareAsync();

        // Assert
        result.IsFirstCycle.ShouldBeFalse();
        result.EntityComparisons.ShouldBeEmpty();
    }

    [Fact]
    public async Task CompareAsync_SpecificCycleId_LoadsSpecificCycle()
    {
        // Arrange
        var current = CreateCycleResult("cycle-curr",
            CreateEntityResult("Customers", EntityStatus.Failed,
                CreateError("Error", "aaaa000000000001")));
        var specific = CreateCycleResult("cycle-specific",
            CreateEntityResult("Customers", EntityStatus.Failed,
                CreateError("Same error", "aaaa000000000001")));

        _repository.GetCycleResultAsync("cycle-curr", Arg.Any<CancellationToken>())
            .Returns(current);
        _repository.GetCycleResultAsync("cycle-specific", Arg.Any<CancellationToken>())
            .Returns(specific);

        // Act
        var result = await _service.CompareAsync(
            currentCycleId: "cycle-curr",
            previousCycleId: "cycle-specific");

        // Assert
        result.CurrentCycleId.ShouldBe("cycle-curr");
        result.PreviousCycleId.ShouldBe("cycle-specific");
        result.EntityComparisons[0].CarryOverErrors.Count.ShouldBe(1);
        await _repository.Received(1).GetCycleResultAsync("cycle-specific", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompareAsync_CurrentCycleIdProvidedPreviousNull_FallsBackToLatest()
    {
        // Arrange -- currentCycleId provided, previousCycleId null: should fall back to GetLatestCycleResultsAsync
        var current = CreateCycleResult("cycle-curr",
            CreateEntityResult("Customers", EntityStatus.Failed,
                CreateError("Error", "aaaa000000000001")));
        var previous = CreateCycleResult("cycle-prev",
            CreateEntityResult("Customers", EntityStatus.Failed,
                CreateError("Same error", "aaaa000000000001")));

        _repository.GetCycleResultAsync("cycle-curr", Arg.Any<CancellationToken>())
            .Returns(current);
        _repository.GetLatestCycleResultsAsync(2, Arg.Any<CancellationToken>())
            .Returns(new List<CycleResult> { current, previous });

        // Act
        var result = await _service.CompareAsync(currentCycleId: "cycle-curr");

        // Assert
        result.CurrentCycleId.ShouldBe("cycle-curr");
        result.PreviousCycleId.ShouldBe("cycle-prev");
        result.EntityComparisons[0].CarryOverErrors.Count.ShouldBe(1);
        await _repository.Received(1).GetCycleResultAsync("cycle-curr", Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().GetCycleResultAsync("cycle-prev", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompareAsync_CalculatesCorrectTotals()
    {
        // Arrange
        var previous = CreateCycleResult("cycle-prev",
            CreateEntityResult("Customers", EntityStatus.Failed,
                CreateError("Carry over error", "aaaa000000000001"),
                CreateError("Resolved error", "bbbb000000000002")),
            CreateEntityResult("Vendors", EntityStatus.Failed,
                CreateError("Vendor resolved", "cccc000000000003")));
        var current = CreateCycleResult("cycle-curr",
            CreateEntityResult("Customers", EntityStatus.Failed,
                CreateError("Carry over error", "aaaa000000000001"),
                CreateError("New error", "dddd000000000004")));

        _repository.GetLatestCycleResultsAsync(2, Arg.Any<CancellationToken>())
            .Returns(new List<CycleResult> { current, previous });

        // Act
        var result = await _service.CompareAsync();

        // Assert
        result.TotalNewErrors.ShouldBe(1);
        result.TotalCarryOverErrors.ShouldBe(1);
        result.TotalResolvedErrors.ShouldBe(2); // bbbb resolved from Customers + cccc resolved from Vendors entity
    }
}
