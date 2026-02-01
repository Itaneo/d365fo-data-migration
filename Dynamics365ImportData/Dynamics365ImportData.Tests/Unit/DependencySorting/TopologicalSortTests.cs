namespace Dynamics365ImportData.Tests.Unit.DependencySorting;

using Dynamics365ImportData.DependencySorting;

using Shouldly;

using Xunit;

public class TopologicalSortTests
{
    [Fact]
    public void CalculateSort_LinearChain_ReturnsCorrectOrder()
    {
        // Arrange
        var graph = new DependencyGraph();
        var a = new OrderedProcess(graph, "A");
        var b = new OrderedProcess(graph, "B");
        var c = new OrderedProcess(graph, "C");
        b.After(a);
        c.After(b);

        // Act
        var sort = graph.CalculateSort();

        // Assert
        var levels = ((IEnumerable<ISet<OrderedProcess>>)sort).ToList();
        levels.Count.ShouldBe(3);
        levels[0].ShouldContain(a);
        levels[1].ShouldContain(b);
        levels[2].ShouldContain(c);
    }

    [Fact]
    public void CalculateSort_DiamondDependency_ReturnsValidOrder()
    {
        // Arrange
        var graph = new DependencyGraph();
        var a = new OrderedProcess(graph, "A");
        var b = new OrderedProcess(graph, "B");
        var c = new OrderedProcess(graph, "C");
        var d = new OrderedProcess(graph, "D");
        b.After(a);
        c.After(a);
        d.After(b);
        d.After(c);

        // Act
        var sort = graph.CalculateSort();

        // Assert
        var levels = ((IEnumerable<ISet<OrderedProcess>>)sort).ToList();
        levels.Count.ShouldBe(3);
        levels[0].ShouldContain(a);
        levels[1].ShouldContain(b);
        levels[1].ShouldContain(c);
        levels[2].ShouldContain(d);
    }

    [Fact]
    public void CalculateSort_CyclicDependency_ThrowsInvalidOperationException()
    {
        // Arrange
        var graph = new DependencyGraph();
        var a = new OrderedProcess(graph, "A");
        var b = new OrderedProcess(graph, "B");
        var c = new OrderedProcess(graph, "C");
        b.After(a);
        c.After(b);
        a.After(c);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => graph.CalculateSort())
            .Message.ShouldBe("Cannot order this set of processes");
    }

    [Fact]
    public void CalculateSort_SingleNode_ReturnsSingleSet()
    {
        // Arrange
        var graph = new DependencyGraph();
        var a = new OrderedProcess(graph, "A");

        // Act
        var sort = graph.CalculateSort();

        // Assert
        var levels = ((IEnumerable<ISet<OrderedProcess>>)sort).ToList();
        levels.Count.ShouldBe(1);
        levels[0].ShouldContain(a);
    }

    [Fact]
    public void CalculateSort_DisconnectedSubgraphs_ReturnsAllNodes()
    {
        // Arrange
        var graph = new DependencyGraph();
        var a = new OrderedProcess(graph, "A");
        var b = new OrderedProcess(graph, "B");
        var c = new OrderedProcess(graph, "C");
        // No dependencies -- all independent

        // Act
        var sort = graph.CalculateSort();

        // Assert
        var levels = ((IEnumerable<ISet<OrderedProcess>>)sort).ToList();
        levels.Count.ShouldBe(1);
        levels[0].Count.ShouldBe(3);
        levels[0].ShouldContain(a);
        levels[0].ShouldContain(b);
        levels[0].ShouldContain(c);
    }
}
