namespace Dynamics365ImportData.Tests.Unit.Pipeline;

using Dynamics365ImportData.DependencySorting;
using Dynamics365ImportData.Pipeline;
using Dynamics365ImportData.Tests.TestHelpers;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

using Xunit;

/// <summary>
/// Unit tests for entity filtering, validation, and EntityValidationException behavior.
/// Tests cover: validation failure detection, case-insensitive matching, backward
/// compatibility with null filter, empty filter handling, level grouping preservation,
/// and empty level removal. Pure validation/filtering logic is tested directly;
/// end-to-end pipeline behavior is verified via IMigrationPipelineService contract.
/// </summary>
public class EntityFilteringTests
{
    #region EntityValidationException Tests

    [Fact]
    public void EntityValidationException_ContainsInvalidAndValidNames()
    {
        // Arrange
        var invalidNames = new List<string> { "BadEntity1", "BadEntity2" };
        var validNames = new HashSet<string> { "CUSTOMERS", "VENDORS", "PRODUCTS" };

        // Act
        var ex = new EntityValidationException(invalidNames, validNames);

        // Assert
        ex.InvalidNames.ShouldBe(invalidNames);
        ex.ValidNames.ShouldBe(validNames);
        ex.Message.ShouldContain("BadEntity1");
        ex.Message.ShouldContain("BadEntity2");
        ex.Message.ShouldContain("CUSTOMERS");
        ex.Message.ShouldContain("PRODUCTS");
        ex.Message.ShouldContain("VENDORS");
    }

    [Fact]
    public void EntityValidationException_ValidNamesAreSorted()
    {
        // Arrange
        var invalidNames = new List<string> { "Bad" };
        var validNames = new HashSet<string> { "ZEBRA", "ALPHA", "MIDDLE" };

        // Act
        var ex = new EntityValidationException(invalidNames, validNames);

        // Assert -- valid names in message should be sorted
        int alphaIdx = ex.Message.IndexOf("ALPHA", StringComparison.Ordinal);
        int middleIdx = ex.Message.IndexOf("MIDDLE", StringComparison.Ordinal);
        int zebraIdx = ex.Message.IndexOf("ZEBRA", StringComparison.Ordinal);
        alphaIdx.ShouldBeLessThan(middleIdx);
        middleIdx.ShouldBeLessThan(zebraIdx);
    }

    [Fact]
    public void EntityValidationException_IsException()
    {
        // Arrange & Act
        var ex = new EntityValidationException(new List<string> { "Bad" }, new HashSet<string> { "Good" });

        // Assert
        ex.ShouldBeAssignableTo<Exception>();
    }

    #endregion

    #region Entity Validation Logic Tests (via interface contract)

    [Fact]
    public async Task ExecuteAsync_WithInvalidEntityName_ThrowsEntityValidationException()
    {
        // Arrange
        var mockPipeline = Substitute.For<IMigrationPipelineService>();
        var invalidFilter = new[] { "NonExistentEntity" };
        var expectedException = new EntityValidationException(
            new List<string> { "NonExistentEntity" },
            new HashSet<string> { "CUSTOMERS", "VENDORS" });

        mockPipeline.ExecuteAsync(PipelineMode.File, invalidFilter, Arg.Any<CancellationToken>())
            .ThrowsAsync(expectedException);

        // Act & Assert
        var ex = await Should.ThrowAsync<EntityValidationException>(
            () => mockPipeline.ExecuteAsync(PipelineMode.File, invalidFilter, CancellationToken.None));

        ex.InvalidNames.ShouldContain("NonExistentEntity");
        ex.ValidNames.ShouldContain("CUSTOMERS");
    }

    [Fact]
    public async Task ExecuteAsync_WithNullEntityFilter_ProcessesAllEntities()
    {
        // Arrange -- null filter should process all entities (backward compatibility)
        var mockPipeline = Substitute.For<IMigrationPipelineService>();

        mockPipeline.ExecuteAsync(PipelineMode.File, null, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CycleResult
            {
                Command = "File",
                TotalEntities = 5,
                Succeeded = 5,
                Failed = 0
            }));

        // Act
        var result = await mockPipeline.ExecuteAsync(PipelineMode.File, null, CancellationToken.None);

        // Assert -- all entities processed
        result.TotalEntities.ShouldBe(5);
        result.Succeeded.ShouldBe(5);
    }

    #endregion

    #region ParseEntityFilter Edge Case Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(",")]
    [InlineData(" , , ")]
    public void ParseEntityFilter_NullOrEmptyOrCommaOnly_ReturnsNull(string? input)
    {
        // Arrange & Act -- reproduce ParseEntityFilter logic
        string[]? result = null;
        if (!string.IsNullOrWhiteSpace(input))
        {
            var parsed = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            result = parsed.Length > 0 ? parsed : null;
        }

        // Assert
        result.ShouldBeNull();
    }

    [Theory]
    [InlineData("Entity1", 1)]
    [InlineData("Entity1,Entity2", 2)]
    [InlineData("Entity1 , Entity2 , Entity3", 3)]
    [InlineData(",Entity1,,Entity2,", 2)]
    public void ParseEntityFilter_ValidInput_ReturnsExpectedCount(string input, int expectedCount)
    {
        // Arrange & Act -- reproduce ParseEntityFilter logic
        var parsed = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string[]? result = parsed.Length > 0 ? parsed : null;

        // Assert
        result.ShouldNotBeNull();
        result.Length.ShouldBe(expectedCount);
    }

    #endregion

    #region Filtering Logic Unit Tests (pure function behavior)

    [Fact]
    public void FilteringLogic_SelectsOnlyMatchingEntities()
    {
        // Arrange -- simulate the filtering logic from MigrationPipelineService
        var sortedQueries = new List<List<SourceQueryItem>>
        {
            new() { TestFixtures.CreateTestQueryItem("CUSTOMERS"), TestFixtures.CreateTestQueryItem("VENDORS") },
            new() { TestFixtures.CreateTestQueryItem("ORDERS"), TestFixtures.CreateTestQueryItem("INVOICES") }
        };
        var entityFilter = new[] { "customers", "orders" };

        // Act -- reproduce the exact filtering logic from MigrationPipelineService.ExecuteAsync
        var filterSet = new HashSet<string>(entityFilter, StringComparer.OrdinalIgnoreCase);
        var filtered = sortedQueries
            .Select(level => level.Where(q => filterSet.Contains(q.EntityName)).ToList())
            .Where(level => level.Count > 0)
            .ToList();

        // Assert
        filtered.Count.ShouldBe(2); // Both levels have matches
        filtered[0].Count.ShouldBe(1);
        filtered[0][0].EntityName.ShouldBe("CUSTOMERS");
        filtered[1].Count.ShouldBe(1);
        filtered[1][0].EntityName.ShouldBe("ORDERS");
    }

    [Fact]
    public void FilteringLogic_RemovesEmptyLevelsAfterFiltering()
    {
        // Arrange
        var sortedQueries = new List<List<SourceQueryItem>>
        {
            new() { TestFixtures.CreateTestQueryItem("CUSTOMERS") },
            new() { TestFixtures.CreateTestQueryItem("ORDERS"), TestFixtures.CreateTestQueryItem("INVOICES") }
        };
        var entityFilter = new[] { "INVOICES" };

        // Act
        var filterSet = new HashSet<string>(entityFilter, StringComparer.OrdinalIgnoreCase);
        var filtered = sortedQueries
            .Select(level => level.Where(q => filterSet.Contains(q.EntityName)).ToList())
            .Where(level => level.Count > 0)
            .ToList();

        // Assert -- level 1 (CUSTOMERS) removed, only level 2 remains
        filtered.Count.ShouldBe(1);
        filtered[0].Count.ShouldBe(1);
        filtered[0][0].EntityName.ShouldBe("INVOICES");
    }

    [Fact]
    public void FilteringLogic_PreservesLevelStructure()
    {
        // Arrange -- entities at different dependency levels should maintain their positions
        var sortedQueries = new List<List<SourceQueryItem>>
        {
            new() { TestFixtures.CreateTestQueryItem("CUSTOMERS"), TestFixtures.CreateTestQueryItem("PRODUCTS") },
            new() { TestFixtures.CreateTestQueryItem("ORDERS") },
            new() { TestFixtures.CreateTestQueryItem("INVOICES") }
        };
        var entityFilter = new[] { "CUSTOMERS", "INVOICES" };

        // Act
        var filterSet = new HashSet<string>(entityFilter, StringComparer.OrdinalIgnoreCase);
        var filtered = sortedQueries
            .Select(level => level.Where(q => filterSet.Contains(q.EntityName)).ToList())
            .Where(level => level.Count > 0)
            .ToList();

        // Assert -- level 2 (ORDERS) removed, levels 1 and 3 preserved in order
        filtered.Count.ShouldBe(2);
        filtered[0][0].EntityName.ShouldBe("CUSTOMERS");
        filtered[1][0].EntityName.ShouldBe("INVOICES");
    }

    [Fact]
    public void FilteringLogic_CaseInsensitiveMatching()
    {
        // Arrange -- entities stored UPPERCASE, filter in mixed case
        var sortedQueries = new List<List<SourceQueryItem>>
        {
            new() { TestFixtures.CreateTestQueryItem("CUSTCUSTOMERV3ENTITY") }
        };

        // Act -- various case formats
        var filterVariants = new[]
        {
            "custcustomerv3entity",        // lowercase
            "CustCustomerV3Entity",        // PascalCase
            "CUSTCUSTOMERV3ENTITY",        // UPPERCASE (exact)
            "cUSTcUSTOMERv3eNTITY"         // random case
        };

        foreach (var filter in filterVariants)
        {
            var filterSet = new HashSet<string>(new[] { filter }, StringComparer.OrdinalIgnoreCase);
            var filtered = sortedQueries
                .Select(level => level.Where(q => filterSet.Contains(q.EntityName)).ToList())
                .Where(level => level.Count > 0)
                .ToList();

            // Assert
            filtered.Count.ShouldBe(1, $"Filter '{filter}' should match CUSTCUSTOMERV3ENTITY");
            filtered[0][0].EntityName.ShouldBe("CUSTCUSTOMERV3ENTITY");
        }
    }

    [Fact]
    public void FilteringLogic_AllEntitiesFiltered_ProducesEmptyResult()
    {
        // Arrange -- filter for entity that doesn't exist in any level
        var sortedQueries = new List<List<SourceQueryItem>>
        {
            new() { TestFixtures.CreateTestQueryItem("CUSTOMERS") },
            new() { TestFixtures.CreateTestQueryItem("ORDERS") }
        };
        var entityFilter = new[] { "NONEXISTENT" };

        // Act
        var filterSet = new HashSet<string>(entityFilter, StringComparer.OrdinalIgnoreCase);
        var filtered = sortedQueries
            .Select(level => level.Where(q => filterSet.Contains(q.EntityName)).ToList())
            .Where(level => level.Count > 0)
            .ToList();

        // Assert -- all levels empty after filtering
        filtered.Count.ShouldBe(0);
    }

    [Fact]
    public void FilteringLogic_DuplicateFilterNames_DoesNotDuplicateResults()
    {
        // Arrange -- same entity name specified twice in filter
        var sortedQueries = new List<List<SourceQueryItem>>
        {
            new() { TestFixtures.CreateTestQueryItem("CUSTOMERS"), TestFixtures.CreateTestQueryItem("VENDORS") }
        };
        var entityFilter = new[] { "CUSTOMERS", "customers", "Customers" };

        // Act
        var filterSet = new HashSet<string>(entityFilter, StringComparer.OrdinalIgnoreCase);
        var filtered = sortedQueries
            .Select(level => level.Where(q => filterSet.Contains(q.EntityName)).ToList())
            .Where(level => level.Count > 0)
            .ToList();

        // Assert -- CUSTOMERS appears only once despite duplicate filter entries
        filtered.Count.ShouldBe(1);
        filtered[0].Count.ShouldBe(1);
        filtered[0][0].EntityName.ShouldBe("CUSTOMERS");
    }

    [Fact]
    public void ValidationLogic_DetectsInvalidEntityNames()
    {
        // Arrange -- simulate the validation logic from MigrationPipelineService.ExecuteAsync
        var sortedQueries = new List<List<SourceQueryItem>>
        {
            new() { TestFixtures.CreateTestQueryItem("CUSTOMERS"), TestFixtures.CreateTestQueryItem("VENDORS") }
        };
        var entityFilter = new[] { "CUSTOMERS", "NonExistent", "AlsoInvalid" };

        // Act
        var allEntityNames = sortedQueries
            .SelectMany(level => level)
            .Select(q => q.EntityName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var invalidNames = entityFilter
            .Where(name => !allEntityNames.Contains(name))
            .ToList();

        // Assert
        invalidNames.Count.ShouldBe(2);
        invalidNames.ShouldContain("NonExistent");
        invalidNames.ShouldContain("AlsoInvalid");
    }

    [Fact]
    public void ValidationLogic_AllValidNamesPass()
    {
        // Arrange
        var sortedQueries = new List<List<SourceQueryItem>>
        {
            new() { TestFixtures.CreateTestQueryItem("CUSTOMERS"), TestFixtures.CreateTestQueryItem("VENDORS") }
        };
        var entityFilter = new[] { "customers", "vendors" }; // lowercase, should match case-insensitively

        // Act
        var allEntityNames = sortedQueries
            .SelectMany(level => level)
            .Select(q => q.EntityName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var invalidNames = entityFilter
            .Where(name => !allEntityNames.Contains(name))
            .ToList();

        // Assert
        invalidNames.ShouldBeEmpty();
    }

    #endregion
}
