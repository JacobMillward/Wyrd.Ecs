using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs;

/// <summary>
/// One archetype's contiguous row range, resolved from an <see cref="ArchetypeQuery"/>. Query
/// resolution covers each matching archetype's full row range; parallel work distribution may
/// subdivide an oversized archetype into consecutive fixed-size ranges (see
/// <see cref="ArchetypeChunks.CollectParallelChunks"/>). Call
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
    private readonly int _start;
    private readonly int _count;

    internal ArchetypeChunk(Archetype archetype, World world)
        : this(archetype, world, 0, archetype.Count)
    {
    }

    /// <summary>
    /// The row range <c>[<paramref name="start"/>, <paramref name="start"/> +
    /// <paramref name="count"/>)</c> of <paramref name="archetype"/>. Consecutive ranges over
    /// one archetype partition its rows exactly once, and every view below is range-relative,
    /// so a slice's index 0 is the slice's first row.
    /// </summary>
    internal ArchetypeChunk(Archetype archetype, World world, int start, int count)
    {
        _archetype = archetype;
        _world = world;
        _start = start;
        _count = count;
    }

    /// <summary>The number of entities in this chunk's row range.</summary>
    public int Count => _count;

    /// <summary>The entities in this chunk's row range, row-aligned with every <see cref="Access{TAccessor}"/> result.</summary>
    public ReadOnlySpan<Entity> Entities => _archetype.Entities.AsSpan(_start, _count);

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
        return TAccessor.CreateChunk(storage.RawItems, storage.RawLastMarkedTick, _world.CurrentTick, _start, _count, tracked);
    }
}
