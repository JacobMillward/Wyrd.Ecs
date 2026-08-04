namespace Wyrd.Ecs.Internal;

/// <summary>
/// Orders a node set so every <see cref="SystemOrderGraph.Edge"/>'s <c>Before</c> node
/// precedes its <c>After</c> node, breaking ties by a caller-supplied order. With zero
/// edges the result is exactly that tie-break order.
/// </summary>
internal static class StableTopologicalSort
{
    /// <summary>
    /// Sorts <paramref name="nodes"/> so every edge's <c>Before</c> node precedes its
    /// <c>After</c> node, breaking every tie by <paramref name="tieBreak"/>. Throws
    /// <see cref="InvalidOperationException"/>, naming the cycle, if the edges are
    /// unsatisfiable.
    /// </summary>
    internal static List<OrderNode> Sort(
        IReadOnlyList<OrderNode> nodes,
        IReadOnlyList<SystemOrderGraph.Edge> edges,
        IReadOnlyDictionary<OrderNode, int> tieBreak)
    {
        var successors = new Dictionary<OrderNode, List<OrderNode>>();
        var inDegree = new Dictionary<OrderNode, int>();
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

        var order = new List<OrderNode>();
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
                $"System ordering forms a cycle: {string.Join(" -> ", cyclePath.Select(n => n.DisplayName))}.");
        }

        return order;
    }

    /// <summary>
    /// Finds one concrete cycle among <paramref name="stuck"/> (the nodes Kahn's algorithm
    /// couldn't fully order). A stuck node isn't necessarily itself a cycle member, since it
    /// may only depend on one, so a single forward walk can dead-end before finding a repeat.
    /// This tries each unexplored stuck node as a fresh start, marking every node a dead-end
    /// walk passed through as visited so it's never re-walked.
    /// </summary>
    private static List<OrderNode> FindCyclePath(
        List<OrderNode> stuck,
        Dictionary<OrderNode, List<OrderNode>> successors)
    {
        var stuckSet = new HashSet<OrderNode>(stuck);
        var visited = new HashSet<OrderNode>();

        foreach (var start in stuck)
        {
            if (visited.Contains(start)) continue;

            var path = new List<OrderNode>();
            var onPath = new HashSet<OrderNode>();
            var current = start;

            while (onPath.Add(current))
            {
                visited.Add(current);
                path.Add(current);

                var foundNext = false;
                var next = default(OrderNode);
                foreach (var candidate in successors[current])
                {
                    if (!stuckSet.Contains(candidate)) continue;
                    next = candidate;
                    foundNext = true;
                    break;
                }
                if (!foundNext) goto NextStart; // dead end: this walk found no cycle

                current = next;
            }

            var cycleStart = path.IndexOf(current);
            return [.. path.Skip(cycleStart), current];

        NextStart:;
        }

        // Unreachable: Sort() only calls this when order.Count < nodes.Count, which
        // guarantees at least one genuine cycle exists among `stuck`.
        throw new InvalidOperationException("System ordering forms a cycle, but the cycle could not be reconstructed.");
    }
}
