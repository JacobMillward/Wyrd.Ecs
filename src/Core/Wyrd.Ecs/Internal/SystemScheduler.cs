namespace Wyrd.Ecs.Internal;

/// <summary>
/// Builds the static parallel schedule: partitions the registered system list into
/// stages honoring two constraints: the conflict rule (no two systems in the same
/// stage share a component type where at least one side writes it) and any Before/After
/// edges declared via <see cref="RunBeforeAttribute"/>/<see cref="RunAfterAttribute"/>/
/// <see cref="OrderedSystem"/>. Computed once, not re-evaluated per tick.
/// </summary>
internal static class SystemScheduler
{
    /// <summary>
    /// Resolves every ordering edge across <paramref name="orderedSystems"/>, stably
    /// topologically sorts the combined node set (tie-broken by registration order), then
    /// packs each node into the first stage at-or-after its minimum-allowed index whose
    /// contents don't conflict with it. Drops marker nodes (see <see cref="OrderNode"/>)
    /// from the result and collapses any stage left with zero real systems.
    /// </summary>
    internal static IReadOnlyList<IReadOnlyList<EcsSystem>> BuildStages(
        IReadOnlyList<OrderedSystem> orderedSystems,
        IReadOnlyDictionary<Type, SystemAccess> generatedAccess)
    {
        var graph = SystemOrderGraph.Resolve(orderedSystems);

        var tieBreak = new Dictionary<OrderNode, int>();
        for (var i = 0; i < orderedSystems.Count; i++)
            tieBreak[OrderNode.ForSystem(orderedSystems[i].System)] = i;
        var syntheticIndex = orderedSystems.Count;
        foreach (var node in graph.Nodes)
            if (!tieBreak.ContainsKey(node))
                tieBreak[node] = syntheticIndex++;

        var order = StableTopologicalSort.Sort(graph.Nodes, graph.Edges, tieBreak);

        var predecessors = new Dictionary<OrderNode, List<OrderNode>>();
        foreach (var node in graph.Nodes) predecessors[node] = [];
        foreach (var edge in graph.Edges) predecessors[edge.After].Add(edge.Before);

        var stages = new List<List<OrderNode>>();
        var stageAccess = new List<(HashSet<Type> Reads, HashSet<Type> Writes)>();
        var stageExclusive = new List<bool>();
        var assignedStage = new Dictionary<OrderNode, int>();

        foreach (var node in order)
        {
            // Predecessors are always assigned before this node (StableTopologicalSort
            // guarantees it), so `minAllowed` never exceeds `stages.Count`, and the
            // "open a new stage" branch below never needs to pad with placeholders.
            var minAllowed = predecessors[node].Count == 0
                ? 0
                : predecessors[node].Max(p => assignedStage[p]) + 1;

            var (reads, writes, exclusive) = node.System is null
                ? ([], [], false)
                : ResolveAccess(node.System, generatedAccess);

            var placedAt = -1;
            if (!exclusive)
            {
                for (var stageIndex = minAllowed; stageIndex < stages.Count; stageIndex++)
                {
                    if (stageExclusive[stageIndex]) continue; // an exclusive stage never accepts a second system

                    var (stageReads, stageWrites) = stageAccess[stageIndex];
                    var conflicts = writes.Overlaps(stageReads) || writes.Overlaps(stageWrites) || reads.Overlaps(stageWrites);
                    if (conflicts) continue;

                    stages[stageIndex].Add(node);
                    stageReads.UnionWith(reads);
                    stageWrites.UnionWith(writes);
                    placedAt = stageIndex;
                    break;
                }
            }

            if (placedAt < 0)
            {
                stages.Add([node]);
                stageAccess.Add(([.. reads], [.. writes]));
                stageExclusive.Add(exclusive);
                placedAt = stages.Count - 1;
            }

            assignedStage[node] = placedAt;
        }

        return stages
            .Select(stage => (IReadOnlyList<EcsSystem>)stage.Where(n => n.System is not null).Select(n => n.System!).ToList())
            .Where(stage => stage.Count > 0)
            .ToList();
    }

    private static (HashSet<Type> Reads, HashSet<Type> Writes, bool Exclusive) ResolveAccess(EcsSystem system, IReadOnlyDictionary<Type, SystemAccess> generatedAccess)
    {
        if (generatedAccess.TryGetValue(system.GetType(), out var access))
            return ([.. access.Reads], [.. access.Writes], false);
        if (system is IQueryAccessDescriptor descriptor)
        {
            var described = descriptor.DescribeAccess();
            return ([.. described.Reads], [.. described.Writes], false);
        }

        // Conservative default: a system with no generated entry or hand-written
        // descriptor never joins an existing stage and never accepts another system
        // into its own.
        return ([], [], true);
    }
}
