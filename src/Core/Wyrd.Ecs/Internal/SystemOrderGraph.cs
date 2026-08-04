namespace Wyrd.Ecs.Internal;

/// <summary>
/// Resolves every Before/After edge declared across a system list — both fluent
/// <see cref="SystemRegistration"/> edges and generator-seeded
/// <see cref="RunBeforeAttribute"/>/<see cref="RunAfterAttribute"/> edges, already
/// unioned into each <see cref="SystemEntry"/>'s own lists by the time this runs — into
/// a graph over <see cref="OrderNode"/>s: every registered system instance, plus one
/// node per distinct <see cref="MarkerSystem"/> type an edge actually references (never
/// an instance of it). Consumed by <see cref="StagePlanner.BuildStages"/>.
/// </summary>
internal static class SystemOrderGraph
{
    internal readonly record struct Edge(OrderNode Before, OrderNode After);

    internal readonly record struct Result(IReadOnlyList<OrderNode> Nodes, IReadOnlyList<Edge> Edges);

    /// <summary>
    /// <paramref name="allRegisteredTypes"/> is purely diagnostic: a caller that
    /// partitions entries by cadence (<see cref="ParallelSystemScheduler"/>) before
    /// calling this can pass the full cross-partition registered-type set so a target
    /// that's registered, just under the other cadence, gets a message saying exactly
    /// that instead of the generic "not currently registered" one. Omitting it (the
    /// default) reproduces today's exact single-partition behavior/message.
    /// </summary>
    internal static Result Resolve(IReadOnlyList<SystemEntry> entries, IReadOnlyCollection<Type>? allRegisteredTypes = null)
    {
        var instancesByType = new Dictionary<Type, List<EcsSystem>>();
        foreach (var entry in entries)
        {
            var type = entry.SystemType;
            if (!instancesByType.TryGetValue(type, out var list))
                instancesByType[type] = list = [];
            list.Add(entry.Instance!);
        }

        var markerNodes = new HashSet<OrderNode>();
        var edges = new List<Edge>();

        OrderNode ResolveTarget(Type target)
        {
            if (typeof(MarkerSystem).IsAssignableFrom(target))
            {
                var node = OrderNode.ForMarker(target);
                markerNodes.Add(node);
                return node;
            }

            if (!typeof(EcsSystem).IsAssignableFrom(target))
                throw new InvalidOperationException(
                    $"'{target}' is used as a system-ordering target but is neither an {nameof(EcsSystem)} nor a {nameof(MarkerSystem)}.");

            if (!instancesByType.TryGetValue(target, out var matches) || matches.Count == 0)
            {
                if (allRegisteredTypes is not null && allRegisteredTypes.Contains(target))
                    throw new InvalidOperationException(
                        $"A system-ordering edge targets '{target}', but it's registered under a different cadence (Fixed vs. Variable) " +
                        "than the system declaring this edge. Cross-cadence ordering edges aren't supported: the fixed-step loop always " +
                        "runs entirely before the variable pass within one World.Update() call, so an edge between the two cadences can't " +
                        "be honored the way a same-cadence edge is. If you need data to flow between them, read the other cadence's " +
                        "last-written component state explicitly instead of ordering across the boundary.");

                throw new InvalidOperationException(
                    $"A system-ordering edge targets '{target}', but no instance of that type is currently registered. " +
                    "This check runs whenever the schedule is recomputed (the next World.Update() call, or an explicit " +
                    "World.FlushSystemChanges()), not at the moment the edge was declared — if this system was meant to " +
                    "register later, make sure it actually does before the next recompute; if it never will, the edge itself is the mistake.");
            }
            if (matches.Count > 1)
                throw new InvalidOperationException(
                    $"A system-ordering edge targets '{target}', but {matches.Count} instances of that type are registered — which one is meant is ambiguous.");

            return OrderNode.ForSystem(matches[0]);
        }

        foreach (var entry in entries)
        {
            var self = OrderNode.ForSystem(entry.Instance!);

            foreach (var beforeTarget in entry.BeforeTargets)
                edges.Add(new Edge(self, ResolveTarget(beforeTarget)));
            foreach (var afterTarget in entry.AfterTargets)
                edges.Add(new Edge(ResolveTarget(afterTarget), self));
        }

        IReadOnlyList<OrderNode> nodes =
        [
            .. entries.Select(e => OrderNode.ForSystem(e.Instance!)),
            .. markerNodes,
        ];

        return new Result(nodes, edges);
    }
}
