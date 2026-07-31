namespace Wyrd.Ecs.Internal;

/// <summary>
/// Orders a node set so every <see cref="SystemOrderGraph.Edge"/>'s <c>Before</c> node
/// precedes its <c>After</c> node, breaking ties by a caller-supplied order. With zero
/// edges the result is exactly that tie-break order — this is what lets
/// <see cref="SystemScheduler.BuildStages"/> reproduce its pre-ordering stage
/// groupings exactly when no system declares any edge.
/// </summary>
internal static class StableTopologicalSort
{
    /// <summary>
    /// Sorts <paramref name="nodes"/> so every edge's <c>Before</c> node precedes its
    /// <c>After</c> node, breaking every tie by <paramref name="tieBreak"/>. Throws
    /// <see cref="InvalidOperationException"/>, naming the cycle, if the edges are
    /// unsatisfiable.
    /// </summary>
    internal static List<SchedulableSystem> Sort(
        IReadOnlyList<SchedulableSystem> nodes,
        IReadOnlyList<SystemOrderGraph.Edge> edges,
        IReadOnlyDictionary<SchedulableSystem, int> tieBreak)
    {
        var successors = new Dictionary<SchedulableSystem, List<SchedulableSystem>>();
        var inDegree = new Dictionary<SchedulableSystem, int>();
        foreach (var node in nodes)
        {
            successors[node] = [];
            inDegree[node] = 0;
        }

        foreach (var edge in edges)
        {
            successors[edge.Before].Add(edge.After);
            inDegree[edge.After]++;
        }

        var ready = nodes.Where(n => inDegree[n] == 0).ToList();
        ready.Sort((x, y) => tieBreak[x].CompareTo(tieBreak[y]));

        var order = new List<SchedulableSystem>();
        while (ready.Count > 0)
        {
            var next = ready[0];
            ready.RemoveAt(0);
            order.Add(next);

            foreach (var successor in successors[next])
            {
                if (--inDegree[successor] != 0) continue;

                var insertAt = ready.FindIndex(n => tieBreak[n] > tieBreak[successor]);
                ready.Insert(insertAt < 0 ? ready.Count : insertAt, successor);
            }
        }

        if (order.Count < nodes.Count)
        {
            var stuck = nodes.Where(n => !order.Contains(n)).ToList();
            var cyclePath = FindCyclePath(stuck, successors);
            throw new InvalidOperationException(
                $"System ordering forms a cycle: {string.Join(" -> ", cyclePath.Select(n => n.GetType().Name))}.");
        }

        return order;
    }

    private static List<SchedulableSystem> FindCyclePath(
        List<SchedulableSystem> stuck,
        Dictionary<SchedulableSystem, List<SchedulableSystem>> successors)
    {
        var stuckSet = new HashSet<SchedulableSystem>(stuck);
        var path = new List<SchedulableSystem>();
        var visited = new HashSet<SchedulableSystem>();
        var current = stuck[0];

        while (visited.Add(current))
        {
            path.Add(current);
            current = successors[current].First(stuckSet.Contains);
        }

        var cycleStart = path.IndexOf(current);
        return [.. path.Skip(cycleStart), current];
    }
}
