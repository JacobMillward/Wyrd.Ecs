using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs;

/// <summary>
/// One archetype's full row range, resolved from an <see cref="ArchetypeQuery"/>. Call
/// <see cref="Access{TAccessor}"/> once per component type your loop needs to read or write.
///
/// <para>Performance tip: when a loop uses two or more <see cref="Access{TAccessor}"/>
/// results together, write the per-row/per-chunk work as a <c>static</c> local function
/// taking the accessors as parameters, rather than inline in the loop body. For example,
/// call <c>Process(chunk.Access&lt;Mut&lt;Position&gt;&gt;(), chunk.Access&lt;Ref&lt;Velocity&gt;&gt;())</c>
/// where <c>Process</c> is <c>static void Process(Mut&lt;Position&gt; position, Ref&lt;Velocity&gt; velocity)</c>.
/// A single-accessor loop needs no such care.</para>
/// </summary>
public readonly struct ArchetypeChunk
{
    private readonly Archetype _archetype;
    private readonly World _world;

    internal ArchetypeChunk(Archetype archetype, World world)
    {
        _archetype = archetype;
        _world = world;
    }

    /// <summary>The number of entities occupying this chunk.</summary>
    public int Count => _archetype.Count;

    /// <summary>The entities occupying this chunk, row-aligned with every <see cref="Access{TAccessor}"/> result.</summary>
    public ReadOnlySpan<Entity> Entities => _archetype.Entities.AsSpan(0, _archetype.Count);

    /// <summary>
    /// Chunk-level access to this archetype's <typeparamref name="TAccessor"/>-wrapped
    /// component column, typically <see cref="Mut{T}"/> or <see cref="Ref{T}"/>. The
    /// archetype must actually store <typeparamref name="TAccessor"/>'s component type
    /// (i.e. this chunk's query included it via <see cref="ArchetypeQuery.Access{TAccessor}"/>).
    /// Calling this for a type the archetype doesn't store is a caller bug: not checked
    /// at runtime.
    /// </summary>
    public TAccessor Access<TAccessor>() where TAccessor : struct, IComponentAccessor<TAccessor>, allows ref struct
    {
        var typeIndex = TAccessor.TypeIndex;
        var storage = _archetype.Storages[typeIndex];
        var tracked = _world.IsTracked(typeIndex);
        return TAccessor.CreateChunk(storage.RawItems, storage.RawLastMarkedTick, _world.CurrentTick, 0, _archetype.Count, tracked);
    }
}
