namespace Dynamics365ImportData.DependencySorting;

using System.Collections.Generic;

/// <summary>
/// A process that requires execution, a process depends upon other processes being executed first,
/// and the resources it uses not being consumed at the same time
/// </summary>
public class OrderedProcess
{
    #region fields

    /// <summary>
    /// The graph this process is part of
    /// </summary>
    public readonly DependencyGraph Graph;

    /// <summary>
    /// The name of this process
    /// </summary>
    public readonly string Name;

    private readonly HashSet<OrderedProcess> _followers = new();
    private readonly HashSet<OrderedProcess> _predecessors = new();
    private readonly HashSet<Resource> _resources = new();

    /// <summary>
    /// Gets the followers of this process
    /// </summary>
    public IEnumerable<OrderedProcess> Followers => _followers;

    /// <summary>
    /// Gets the predecessors of this process
    /// </summary>
    /// <value>The predecessors.</value>
    public IEnumerable<OrderedProcess> Predecessors => _predecessors;

    /// <summary>
    /// Gets the resources this process depends upon
    /// </summary>
    /// <value>The resources.</value>
    public IEnumerable<Resource> Resources => _resources;

    internal ISet<Resource> ResourcesSet => _resources;

    #endregion fields

    #region constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderedProcess" /> class.
    /// </summary>
    /// <param name="graph">The graph which this process is part of</param>
    /// <param name="name">The name of this process</param>
    public OrderedProcess(DependencyGraph graph, string name)
    {
        Graph = graph;
        Name = name;

        _ = Graph.Add(this);
    }

    #endregion constructor

    #region ordering constraints

    /// <summary>
    /// Indicates that this process should execute after another
    /// </summary>
    /// <param name="predecessor">The predecessor.</param>
    /// <returns>returns this process</returns>
    public OrderedProcess After(OrderedProcess predecessor)
    {
        DependencyGraph.CheckGraph(this, predecessor);

        if (_predecessors.Add(predecessor))
        {
            _ = predecessor.Before(this);
        }

        return predecessor;
    }

    /// <summary>
    /// Indicates that this process must happen after all the predecessors
    /// </summary>
    /// <param name="predecessors">The predecessors.</param>
    /// <returns>the predecessors</returns>
    public IEnumerable<OrderedProcess> After(params OrderedProcess[] predecessors)
    {
        return After(predecessors as IEnumerable<OrderedProcess>);
    }

    /// <summary>
    /// Indicates that this process must happen after all the predecessors
    /// </summary>
    /// <param name="predecessors">The predecessors.</param>
    /// <returns>the predecessors</returns>
    public IEnumerable<OrderedProcess> After(IEnumerable<OrderedProcess> predecessors)
    {
        foreach (OrderedProcess predecessor in predecessors)
        {
            _ = After(predecessor);
        }

        return predecessors;
    }

    /// <summary>
    /// Indicates that this process should execute before another
    /// </summary>
    /// <param name="follower">The ancestor.</param>
    /// <returns>returns this process</returns>
    public OrderedProcess Before(OrderedProcess follower)
    {
        DependencyGraph.CheckGraph(this, follower);

        if (_followers.Add(follower))
        {
            _ = follower.After(this);
        }

        return follower;
    }

    /// <summary>
    /// Indicates that this process must happen before all the followers
    /// </summary>
    /// <param name="followers">The followers.</param>
    /// <returns>the followers</returns>
    public IEnumerable<OrderedProcess> Before(params OrderedProcess[] followers)
    {
        return Before(followers as IEnumerable<OrderedProcess>);
    }

    /// <summary>
    /// Indicates that this process must happen before all the followers
    /// </summary>
    /// <param name="followers">The followers.</param>
    /// <returns>the followers</returns>
    public IEnumerable<OrderedProcess> Before(IEnumerable<OrderedProcess> followers)
    {
        foreach (OrderedProcess ancestor in followers)
        {
            _ = Before(ancestor);
        }

        return followers;
    }

    #endregion ordering constraints

    #region resource constraints

    /// <summary>
    /// Indicates that this process requires the specified resource.
    /// </summary>
    /// <param name="resource">The resource.</param>
    /// <returns>returns this process</returns>
    public void Requires(Resource resource)
    {
        DependencyGraph.CheckGraph(resource, this);

        if (_resources.Add(resource))
        {
            resource.UsedBy(this);
        }
    }

    #endregion resource constraints

    /// <summary>
    /// Returns a <see cref="System.String" /> that represents this instance.
    /// </summary>
    /// <returns>A <see cref="System.String" /> that represents this instance.</returns>
    public override string ToString()
    {
        return "Process { " + Name + " }";
    }
}