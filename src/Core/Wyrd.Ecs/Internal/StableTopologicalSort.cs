namespace Wyrd.Ecs.Internal;

/// <summary>
/// Orders a node set so every edge's <c>Before</c> node precedes its <c>After</c> node,
/// breaking ties by a caller-supplied order. With zero edges the result is exactly that
/// tie-break order. Generic over the node type so both the post-construction
/// system-ordering graph (<see cref="OrderNode"/>, resolved from live <see cref="EcsSystem"/>
/// instances/marker types by <see cref="SystemOrderGraph"/>) and <see cref="WorldBuilder"/>'s
/// pre-construction dependency graph (bare <see cref="Type"/>) share one implementation - the
/// algorithm itself never inspects what a node represents, only that it's usable as a
/// dictionary key.
/// </summary>
internal static class StableTopologicalSort
{
    internal readonly record struct Edge<TNode>(TNode Before, TNode After) where TNode : notnull;

    /// <summary>
    /// Sorts <paramref name="nodes"/> so every edge's <c>Before</c> node precedes its
    /// <c>After</c> node, breaking every tie by <paramref name="tieBreak"/>. Throws
    /// <see cref="InvalidOperationException"/>, naming the cycle via <paramref name="displayName"/>,
    /// if the edges are unsatisfiable. Every <c>TNode</c> referenced by an edge must already
    /// be present in <paramref name="nodes"/> - the caller validates that before calling this
    /// (see <see cref="SystemOrderGraph.Resolve"/>'s <c>ResolveTarget</c> for the existing
    /// example), since this method has no way to produce a friendly "unregistered type"
    /// message once it's indexing straight into <c>successors</c>.
    /// </summary>
    internal static List<TNode> Sort<TNode>(
        IReadOnlyList<TNode> nodes,
        IReadOnlyList<Edge<TNode>> edges,
        IReadOnlyDictionary<TNode, int> tieBreak,
        Func<TNode, string> displayName)
        where TNode : notnull
    {
        var successors = new Dictionary<TNode, List<TNode>>();
        var inDegree = new Dictionary<TNode, int>();
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

        var order = new List<TNode>();
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
                $"System ordering forms a cycle: {string.Join(" -> ", cyclePath.Select(displayName))}.");
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
    private static List<TNode> FindCyclePath<TNode>(
        List<TNode> stuck,
        Dictionary<TNode, List<TNode>> successors)
        where TNode : notnull
    {
        var stuckSet = new HashSet<TNode>(stuck);
        var visited = new HashSet<TNode>();

        foreach (var start in stuck)
        {
            if (visited.Contains(start)) continue;

            var path = new List<TNode>();
            var onPath = new HashSet<TNode>();
            var current = start;

            while (onPath.Add(current))
            {
                visited.Add(current);
                path.Add(current);

                var foundNext = false;
                var next = default(TNode)!;
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
