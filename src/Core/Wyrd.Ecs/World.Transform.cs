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
    /// does for a missing component. Does not guard against a <see cref="Parent"/> cycle: one
    /// recursive call per ancestor, same caller-error scenario <see cref="Ancestors"/> already
    /// documents.
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

    /// <inheritdoc cref="GetWorldTransform"/>
    public WorldTransform GetInterpolatedWorldTransform(Entity entity)
    {
        var (current, previous) = ComposeInterpolated(entity);
        var alpha = (float)FixedStepAlpha;
        return new WorldTransform(
            Vector3.Lerp(previous.Position, current.Position, alpha),
            Quaternion.Slerp(previous.Rotation, current.Rotation, alpha),
            Vector3.Lerp(previous.Scale, current.Scale, alpha));
    }

    /// <summary>Composes both the current and the previous world transform in one walk of the <see cref="Parent"/> chain.</summary>
    private (WorldTransform Current, WorldTransform Previous) ComposeInterpolated(Entity entity)
    {
        var current = GetComponent<Transform>(entity);
        var previous = GetComponent<PreviousTransform>(entity);

        if (!TryGetParent(entity, out var parent))
            return (
                new WorldTransform(current.Position, current.Rotation, current.Scale),
                new WorldTransform(previous.Position, previous.Rotation, previous.Scale));

        var (parentCurrent, parentPrevious) = ComposeInterpolated(parent);
        return (
            Compose(parentCurrent, current.Position, current.Rotation, current.Scale),
            Compose(parentPrevious, previous.Position, previous.Rotation, previous.Scale));
    }
}
