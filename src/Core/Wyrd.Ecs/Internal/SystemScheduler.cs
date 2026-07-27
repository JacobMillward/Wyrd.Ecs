namespace Wyrd.Ecs.Internal;

/// <summary>
/// Builds the static parallel schedule: greedily partitions a fixed system list into
/// stages where no two systems in the same stage conflict (share a component type
/// where at least one side writes it) — <c>Has</c>/<c>Without</c>/<c>Any</c> elements
/// never contribute, since they're filter-only. Computed once, not re-evaluated per
/// tick; the caller (<c>WorldBuilder.Build</c>) is responsible for that.
/// </summary>
internal static class SystemScheduler
{
    /// <summary>
    /// Partitions <paramref name="systems"/> into stages, processed in the given order
    /// (the caller's responsibility to pass a stable one). Each system's access comes
    /// from <paramref name="generatedAccess"/> first, then <see cref="IQueryAccessDescriptor"/>
    /// if it implements that, then a conservative "give it its own exclusive stage" if
    /// neither applies — tracked as an actual exclusivity flag
    /// (<c>ResolveAccess</c>'s <c>Exclusive</c> result), not as synthetic Reads/Writes
    /// data: a plain set-overlap check can only ever make an unknown system conflict
    /// with something that <em>also</em> happens to touch the same synthetic marker, never
    /// with an arbitrary other system's real (and otherwise disjoint) access — confirmed
    /// wrong the hard way, by a failing test, before landing on this instead.
    /// </summary>
    internal static IReadOnlyList<IReadOnlyList<EcsSystem>> BuildStages(
        IReadOnlyList<EcsSystem> systems,
        IReadOnlyDictionary<Type, SystemAccess> generatedAccess)
    {
        var stages = new List<List<EcsSystem>>();
        var stageAccess = new List<(HashSet<Type> Reads, HashSet<Type> Writes)>();
        var stageExclusive = new List<bool>();

        for (var i = 0; i < systems.Count; i++)
        {
            var (reads, writes, exclusive) = ResolveAccess(systems[i], generatedAccess);

            var placed = false;
            if (!exclusive)
            {
                for (var stageIndex = 0; stageIndex < stages.Count; stageIndex++)
                {
                    if (stageExclusive[stageIndex]) continue; // an exclusive stage never accepts a second system

                    var (stageReads, stageWrites) = stageAccess[stageIndex];
                    var conflicts = writes.Overlaps(stageReads) || writes.Overlaps(stageWrites) || reads.Overlaps(stageWrites);
                    if (conflicts) continue;

                    stages[stageIndex].Add(systems[i]);
                    stageReads.UnionWith(reads);
                    stageWrites.UnionWith(writes);
                    placed = true;
                    break;
                }
            }

            if (!placed)
            {
                stages.Add([systems[i]]);
                stageAccess.Add(([.. reads], [.. writes]));
                stageExclusive.Add(exclusive);
            }
        }

        return stages;
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

        // Conservative default: a system with neither a generated entry nor a
        // hand-written descriptor never joins an existing stage and never accepts
        // another system into its own -- see BuildStages' doc for why this can't be
        // expressed as synthetic Reads/Writes data instead.
        return ([], [], true);
    }
}
