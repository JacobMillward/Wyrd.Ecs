namespace Wyrd.Ecs.Internal;

/// <summary>
/// Cleans up the mirrored side of one relation-storage row when the entity holding it is
/// destroyed — e.g. for a <c>RelationLinks&lt;Likes&gt;</c> row, remove <paramref name="self"/>
/// from each of its targets' <c>RelationBacklinks&lt;Likes&gt;</c>.
/// </summary>
internal delegate void RelationCascadeHandler(World world, Entity self, IComponentStorage storage, int row);

/// <summary>
/// A type-erased dispatch table from a relation wrapper type's <see cref="TypeIndex{T}"/>
/// to its cascade-cleanup handler, so <see cref="World"/>'s entity-destroy path can walk
/// a destroyed entity's components and ask "are you a relation storage, and if so, clean
/// up your mirror" without knowing every closed <c>RelationLinks&lt;T&gt;</c>/etc. at
/// compile time — mirrors <c>CommandBuffer.IResettableBuffer</c>'s reasoning. Each of the
/// four relation wrapper types (<c>RelationLinks&lt;T&gt;</c>, <c>RelationBacklinks&lt;T&gt;</c>,
/// <c>RelationTagLinks&lt;T&gt;</c>, <c>RelationTagBacklinks&lt;T&gt;</c>) registers its own
/// handler from its own static constructor, guaranteed to run before any entity could ever
/// have one (the very first thing any relation operation does is read that closed type's
/// own <see cref="TypeIndex{T}"/>, which is what triggers the static constructor).
/// </summary>
internal static class RelationRegistry
{
    /// <summary>
    /// Copy-on-write, not <see cref="ArrayGrowth.EnsureCapacity{T}"/>'s usual in-place
    /// resize: two different relation types' static constructors can run concurrently on
    /// different threads (two systems in the same scheduled stage, each first touching a
    /// different relation type at once), and <see cref="Get"/> runs on every entity-destroy's
    /// per-component loop — including for entities with zero relations — so it must stay
    /// lock-free rather than pay <see cref="_registerGate"/>'s cost on that path.
    /// <see cref="Register"/> is the only writer, always under <see cref="_registerGate"/>,
    /// and always publishes a brand-new array via a single reference assignment; a
    /// concurrent lock-free <see cref="Get"/> either sees the array from before or after
    /// a given <see cref="Register"/> call, never a partially-written one.
    /// </summary>
    private static RelationCascadeHandler?[] _handlers = new RelationCascadeHandler?[4];

    private static readonly Lock _registerGate = new();

    internal static void Register(int typeIndex, RelationCascadeHandler handler)
    {
        lock (_registerGate)
        {
            var current = _handlers;
            var next = new RelationCascadeHandler?[Math.Max(typeIndex + 1, current.Length)];
            Array.Copy(current, next, current.Length);
            next[typeIndex] = handler;
            _handlers = next;
        }
    }

    internal static RelationCascadeHandler? Get(int typeIndex)
    {
        var handlers = _handlers;
        return typeIndex < handlers.Length ? handlers[typeIndex] : null;
    }
}
