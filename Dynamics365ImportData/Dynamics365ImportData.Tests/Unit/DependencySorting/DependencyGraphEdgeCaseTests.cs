namespace Dynamics365ImportData.Tests.Unit.DependencySorting;

using Dynamics365ImportData.DependencySorting;

using Shouldly;

using Xunit;

public class DependencyGraphEdgeCaseTests
{
    [Fact]
    public void CalculateSort_EmptyGraph_ThrowsInvalidOperationException()
    {
        // Arrange
        var graph = new DependencyGraph();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => graph.CalculateSort())
            .Message.ShouldBe("Cannot order this set of processes");
    }

    [Fact]
    public void CalculateSort_DisconnectedSubgraphs_ReturnsAllNodesInValidOrder()
    {
        // Arrange -- A→B and C→D are independent chains
        var graph = new DependencyGraph();
        var a = new OrderedProcess(graph, "A");
        var b = new OrderedProcess(graph, "B");
        var c = new OrderedProcess(graph, "C");
        var d = new OrderedProcess(graph, "D");
        b.After(a);
        d.After(c);

        // Act
        var sort = graph.CalculateSort();

        // Assert
        var levels = ((IEnumerable<ISet<OrderedProcess>>)sort).ToList();
        levels.Count.ShouldBe(2);
        levels[0].ShouldContain(a);
        levels[0].ShouldContain(c);
        levels[1].ShouldContain(b);
        levels[1].ShouldContain(d);
    }

    [Fact]
    public void CalculateSort_LargeGraph_CompletesWithoutStackOverflow()
    {
        // Arrange -- 100+ nodes in a long chain
        var graph = new DependencyGraph();
        var nodes = new List<OrderedProcess>();
        for (int i = 0; i < 150; i++)
        {
            nodes.Add(new OrderedProcess(graph, $"Node{i}"));
        }
        for (int i = 1; i < nodes.Count; i++)
        {
            nodes[i].After(nodes[i - 1]);
        }

        // Act
        var sort = graph.CalculateSort();

        // Assert
        var levels = ((IEnumerable<ISet<OrderedProcess>>)sort).ToList();
        levels.Count.ShouldBe(150);
        levels[0].ShouldContain(nodes[0]);
        levels[149].ShouldContain(nodes[149]);
    }

    [Fact]
    public void CalculateSort_DeterministicOrdering_ProducesSameResultOnRepeatedCalls()
    {
        // Arrange
        List<List<string>> CollectLevelNames()
        {
            var graph = new DependencyGraph();
            var a = new OrderedProcess(graph, "A");
            var b = new OrderedProcess(graph, "B");
            var c = new OrderedProcess(graph, "C");
            var d = new OrderedProcess(graph, "D");
            b.After(a);
            c.After(a);
            d.After(b);
            d.After(c);

            var sort = graph.CalculateSort();
            return ((IEnumerable<ISet<OrderedProcess>>)sort)
                .Select(level => level.Select(p => p.Name).OrderBy(n => n).ToList())
                .ToList();
        }

        // Act -- run CalculateSort 10 times
        var firstResult = CollectLevelNames();
        for (int i = 1; i < 10; i++)
        {
            var currentResult = CollectLevelNames();

            // Assert
            currentResult.Count.ShouldBe(firstResult.Count);
            for (int j = 0; j < firstResult.Count; j++)
            {
                currentResult[j].ShouldBe(firstResult[j]);
            }
        }
    }

    [Fact]
    public void CalculateSort_DiamondWithExtraDependencies_ReturnsValidOrder()
    {
        // Arrange -- A→B, A→C, B→D, C→D, E→D (complex diamond variant)
        var graph = new DependencyGraph();
        var a = new OrderedProcess(graph, "A");
        var b = new OrderedProcess(graph, "B");
        var c = new OrderedProcess(graph, "C");
        var d = new OrderedProcess(graph, "D");
        var e = new OrderedProcess(graph, "E");
        b.After(a);
        c.After(a);
        d.After(b);
        d.After(c);
        d.After(e);

        // Act
        var sort = graph.CalculateSort();

        // Assert
        var levels = ((IEnumerable<ISet<OrderedProcess>>)sort).ToList();
        var allNodes = levels.SelectMany(l => l).ToList();
        allNodes.Count.ShouldBe(5);

        // D must come after A, B, C, and E
        var dIndex = levels.FindIndex(l => l.Contains(d));
        var aIndex = levels.FindIndex(l => l.Contains(a));
        var bIndex = levels.FindIndex(l => l.Contains(b));
        var cIndex = levels.FindIndex(l => l.Contains(c));
        var eIndex = levels.FindIndex(l => l.Contains(e));
        dIndex.ShouldBeGreaterThan(aIndex);
        dIndex.ShouldBeGreaterThan(bIndex);
        dIndex.ShouldBeGreaterThan(cIndex);
        dIndex.ShouldBeGreaterThan(eIndex);
    }

    [Fact]
    public void CalculateSort_ParallelIndependentNodes_AllInSameLevel()
    {
        // Arrange -- 5 processes with no dependencies
        var graph = new DependencyGraph();
        var nodes = new List<OrderedProcess>();
        for (int i = 0; i < 5; i++)
        {
            nodes.Add(new OrderedProcess(graph, $"Independent{i}"));
        }

        // Act
        var sort = graph.CalculateSort();

        // Assert -- all should be in first (and only) level
        var levels = ((IEnumerable<ISet<OrderedProcess>>)sort).ToList();
        levels.Count.ShouldBe(1);
        levels[0].Count.ShouldBe(5);
        foreach (var node in nodes)
        {
            levels[0].ShouldContain(node);
        }
    }
}
