using System.Numerics;

namespace Wyrd.Ecs;

/// <summary>A composed, world-space position/rotation/scale: the result of walking an entity's <see cref="Parent"/> chain, not itself a component.</summary>
public readonly record struct WorldTransform(Vector3 Position, Quaternion Rotation, Vector3 Scale);

public sealed partial class World
{
    /// <summary>
    /// <paramref name="entity"/>'s <see cref="Transform"/> composed with every ancestor's.
    /// Recurses up the <see cref="Parent"/> chain directly via <see cref="TryGetParent"/>
    /// rather than materializing it into a list first: both that lookup and
    /// <see cref="GetComponent{T}(Entity)"/> are already allocation-free (dictionary lookups
    /// against storage the archetype already owns), so this makes the whole call zero
    /// heap allocations, not just avoids one extra list. No caching across calls, so
    /// deliberately not the right choice to call many times per frame without a caller-side
    /// cache; revisit if profiling shows that matters. An entity with no
    /// <see cref="Transform"/> throws the same way <see cref="GetComponent{T}(Entity)"/> already
    /// does for a missing component.
    /// </summary>
    public WorldTransform GetWorldTransform(Entity entity)
    {
        var local = GetComponent<Transform>(entity);
        if (!TryGetParent(entity, out var parent))
            return new WorldTransform(local.Position, local.Rotation, local.Scale);

        return Compose(GetWorldTransform(parent), local.Position, local.Rotation, local.Scale);
    }

    /// <summary>Applies a local position/rotation/scale on top of an already-composed parent world transform.</summary>
    private static WorldTransform Compose(WorldTransform parent, Vector3 localPosition, Quaternion localRotation, Vector3 localScale) =>
        new(
            parent.Position + Vector3.Transform(localPosition * parent.Scale, parent.Rotation),
            parent.Rotation * localRotation,
            parent.Scale * localScale);
}
