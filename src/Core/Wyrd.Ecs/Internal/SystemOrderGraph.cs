using System.Reflection;

namespace Wyrd.Ecs.Internal;

/// <summary>
/// Resolves every Before/After edge declared across a system list, via
/// <see cref="RunBeforeAttribute"/>/<see cref="RunAfterAttribute"/> and
/// <see cref="OrderedSystem"/>'s fluent wrapper, into a graph over
/// <see cref="OrderNode"/>s: every registered <see cref="EcsSystem"/> instance, plus
/// one node per distinct <see cref="MarkerSystem"/> type an edge actually references
/// (never an instance of it). Consumed by <see cref="StagePlanner.BuildStages"/>.
/// </summary>
internal static class SystemOrderGraph
{
    internal readonly record struct Edge(OrderNode Before, OrderNode After);

    internal readonly record struct Result(IReadOnlyList<OrderNode> Nodes, IReadOnlyList<Edge> Edges);

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
                throw new InvalidOperationException(
                    $"A system-ordering edge targets '{target}', but no instance of that type is registered.");
            if (matches.Count > 1)
                throw new InvalidOperationException(
                    $"A system-ordering edge targets '{target}', but {matches.Count} instances of that type are registered — which one is meant is ambiguous.");

            return OrderNode.ForSystem(matches[0]);
        }

        foreach (var ordered in orderedSystems)
        {
            var self = OrderNode.ForSystem(ordered.System);

            foreach (var beforeTarget in ordered.BeforeTargets)
                edges.Add(new Edge(self, ResolveTarget(beforeTarget)));
            foreach (var afterTarget in ordered.AfterTargets)
                edges.Add(new Edge(ResolveTarget(afterTarget), self));

            foreach (var attribute in ordered.System.GetType().GetCustomAttributes<RunBeforeAttribute>())
                edges.Add(new Edge(self, ResolveTarget(attribute.Target)));
            foreach (var attribute in ordered.System.GetType().GetCustomAttributes<RunAfterAttribute>())
                edges.Add(new Edge(ResolveTarget(attribute.Target), self));
        }

        IReadOnlyList<OrderNode> nodes =
        [
            .. orderedSystems.Select(o => OrderNode.ForSystem(o.System)),
            .. markerNodes,
        ];

        return new Result(nodes, edges);
    }
}
