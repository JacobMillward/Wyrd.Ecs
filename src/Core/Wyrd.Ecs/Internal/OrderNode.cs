using System.Runtime.CompilerServices;

namespace Wyrd.Ecs.Internal;

/// <summary>
/// One node in the system-ordering graph: either a registered <see cref="EcsSystem"/>
/// instance, or a bare marker <see cref="Type"/> (never a <see cref="MarkerSystem"/>
/// instance, since a marker is only ever a unique ordering token, never constructed).
/// Equality is by reference for a system, by <see cref="Type"/> for a marker, so every
/// edge targeting the same marker type resolves to the same node.
/// </summary>
internal readonly struct OrderNode : IEquatable<OrderNode>
{
    internal EcsSystem? System { get; }
    private readonly Type? _markerType;

    private OrderNode(EcsSystem? system, Type? markerType)
    {
        System = system;
        _markerType = markerType;
    }

    internal static OrderNode ForSystem(EcsSystem system) => new(system, null);
    internal static OrderNode ForMarker(Type markerType) => new(null, markerType);

    internal string DisplayName => _markerType?.Name ?? System!.GetType().Name;

    public bool Equals(OrderNode other) =>
        _markerType is not null ? _markerType == other._markerType : ReferenceEquals(System, other.System);

    public override bool Equals(object? obj) => obj is OrderNode other && Equals(other);

    public override int GetHashCode() =>
        _markerType?.GetHashCode() ?? RuntimeHelpers.GetHashCode(System);

    public static bool operator ==(OrderNode left, OrderNode right) => left.Equals(right);
    public static bool operator !=(OrderNode left, OrderNode right) => !left.Equals(right);
}
