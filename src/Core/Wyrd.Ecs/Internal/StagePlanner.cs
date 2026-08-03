namespace Wyrd.Ecs.Internal;

/// <summary>
/// Builds the static parallel schedule: partitions the registered system list into
/// stages honoring two constraints: the conflict rule (no two systems in the same
/// stage share a component type where at least one side writes it) and any Before/After
/// edges declared via <see cref="RunBeforeAttribute"/>/<see cref="RunAfterAttribute"/>/
/// <see cref="SystemRegistration"/>. Computed once per call — a caller that needs a
/// fresh schedule after a structural change (see <see cref="ParallelSystemScheduler"/>)
/// simply calls this again over the current full entry list, rather than patching a
/// previous result.
/// </summary>
internal static class StagePlanner
{
    /// <summary>
    /// Resolves every ordering edge across <paramref name="entries"/>, stably
    /// topologically sorts the combined node set (tie-broken by registration order), then
    /// packs each node into the first stage at-or-after its minimum-allowed index whose
    /// contents don't conflict with it. Drops marker nodes (see <see cref="OrderNode"/>)
    /// from the result and collapses any stage left with zero real systems.
    /// </summary>
    internal static IReadOnlyList<IReadOnlyList<EcsSystem>> BuildStages(IReadOnlyList<SystemEntry> entries)
    {
        var graph = SystemOrderGraph.Resolve(entries);

        var tieBreak = new Dictionary<OrderNode, int>();
        for (var i = 0; i < entries.Count; i++)
            tieBreak[OrderNode.ForSystem(entries[i].Instance!)] = i;
        var syntheticIndex = entries.Count;
        foreach (var node in graph.Nodes)
            if (!tieBreak.ContainsKey(node))
                tieBreak[node] = syntheticIndex++;

        var order = StableTopologicalSort.Sort(graph.Nodes, graph.Edges, tieBreak);

        var predecessors = new Dictionary<OrderNode, List<OrderNode>>();
        foreach (var node in graph.Nodes) predecessors[node] = [];
        foreach (var edge in graph.Edges) predecessors[edge.After].Add(edge.Before);

        // Built via indexer assignment, not ToDictionary: every real registration path
        // (WorldBuilder/World's AddSystemCore, ParallelSystemScheduler.Register) now
        // rejects a second instance of the same system Type at registration time, so in
        // practice this dictionary never sees two different Access values for one Type.
        // The one place a duplicate Type can still legitimately reach here is a test
        // exercising this algorithm directly against hand-built SystemEntry arrays
        // (StagePlannerTests) — for that case, indexer assignment (last one wins) is a
        // deliberately permissive default rather than a defensive check this pure
        // function has no way to act on anyway (it can't reject anything; it can only
        // return a schedule).
        var accessByType = new Dictionary<Type, SystemAccess>();
        foreach (var entry in entries)
            if (entry.Access is not null)
                accessByType[entry.SystemType] = entry.Access;

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
                : ResolveAccess(node.System, accessByType);

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

    private static (HashSet<Type> Reads, HashSet<Type> Writes, bool Exclusive) ResolveAccess(EcsSystem system, IReadOnlyDictionary<Type, SystemAccess> accessByType)
    {
        if (accessByType.TryGetValue(system.GetType(), out var access))
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
