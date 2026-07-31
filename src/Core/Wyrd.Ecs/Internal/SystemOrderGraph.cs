using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Wyrd.Ecs.Internal;

/// <summary>
/// Resolves every Before/After edge declared across a system list — via
/// <see cref="RunBeforeAttribute"/>/<see cref="RunAfterAttribute"/> and
/// <see cref="OrderedSystem"/>'s fluent wrapper — into a graph over concrete
/// <see cref="SchedulableSystem"/> nodes: every registered <see cref="EcsSystem"/>
/// instance, plus one synthesized <see cref="MarkerSystem"/> instance per marker type
/// an edge actually references. Consumed by <see cref="SystemScheduler.BuildStages"/>
/// to seed each node's minimum-allowed stage index before conflict-based packing runs.
/// </summary>
internal static class SystemOrderGraph
{
    internal readonly record struct Edge(SchedulableSystem Before, SchedulableSystem After);

    internal readonly record struct Result(IReadOnlyList<SchedulableSystem> Nodes, IReadOnlyList<Edge> Edges);

    internal static Result Resolve(IReadOnlyList<OrderedSystem> orderedSystems)
    {
        var instancesByType = new Dictionary<Type, List<EcsSystem>>();
        foreach (var ordered in orderedSystems)
        {
            var type = ordered.System.GetType();
            if (!instancesByType.TryGetValue(type, out var list))
                instancesByType[type] = list = [];
            list.Add(ordered.System);
        }

        var markersByType = new Dictionary<Type, MarkerSystem>();
        var edges = new List<Edge>();

        [UnconditionalSuppressMessage("Trimming", "IL2067",
            Justification = "A MarkerSystem subtype is, by construction (see MarkerSystem's own doc comment), a one-line declaration with no members to trim away, including its constructor -- there is nothing for the trimmer to have removed.")]
        SchedulableSystem ResolveTarget(Type target)
        {
            if (typeof(MarkerSystem).IsAssignableFrom(target))
            {
                if (!markersByType.TryGetValue(target, out var marker))
                    markersByType[target] = marker = (MarkerSystem)Activator.CreateInstance(target)!;
                return marker;
            }

            if (!typeof(EcsSystem).IsAssignableFrom(target))
                throw new InvalidOperationException(
                    $"'{target}' is used as a system-ordering target but is neither an {nameof(EcsSystem)} nor a {nameof(MarkerSystem)}.");

            if (!instancesByType.TryGetValue(target, out var matches) || matches.Count == 0)
                throw new InvalidOperationException(
                    $"A system-ordering edge targets '{target}', but no instance of that type is registered.");
            if (matches.Count > 1)
                throw new InvalidOperationException(
                    $"A system-ordering edge targets '{target}', but {matches.Count} instances of that type are registered — which one is meant is ambiguous.");

            return matches[0];
        }

        foreach (var ordered in orderedSystems)
        {
            foreach (var beforeTarget in ordered.BeforeTargets)
                edges.Add(new Edge(ordered.System, ResolveTarget(beforeTarget)));
            foreach (var afterTarget in ordered.AfterTargets)
                edges.Add(new Edge(ResolveTarget(afterTarget), ordered.System));

            foreach (var attribute in ordered.System.GetType().GetCustomAttributes<RunBeforeAttribute>())
                edges.Add(new Edge(ordered.System, ResolveTarget(attribute.Target)));
            foreach (var attribute in ordered.System.GetType().GetCustomAttributes<RunAfterAttribute>())
                edges.Add(new Edge(ResolveTarget(attribute.Target), ordered.System));
        }

        IReadOnlyList<SchedulableSystem> nodes =
        [
            .. orderedSystems.Select(o => (SchedulableSystem)o.System),
            .. markersByType.Values,
        ];

        return new Result(nodes, edges);
    }
}
