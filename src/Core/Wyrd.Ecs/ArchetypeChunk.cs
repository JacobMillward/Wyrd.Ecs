using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs;

/// <summary>
/// One archetype's full row range, resolved from an <see cref="ArchetypeQuery"/>. Every
/// caller — <see cref="World.Query{TAccess0}(ChunkAction{TAccess0})"/>'s hand-written
/// implementation and, eventually, generator-emitted query code of any arity alike — calls
/// one <see cref="Access{TAccessor}"/> per required component type, using the same flat,
/// non-recursive style throughout: there is no per-shape specialization at this layer.
///
/// <para>Performance note, confirmed by benchmark (<c>QueryIterationBenchmarks</c> in this
/// repo's benchmark suite): when a loop uses two or more <see cref="Access{TAccessor}"/>
/// results together, write the per-row/per-chunk work as a <c>static</c> local function
/// taking the accessors as parameters, rather than inline in the enclosing loop —
/// e.g. <c>Process(chunk.Access&lt;Mut&lt;Position&gt;&gt;(), chunk.Access&lt;Ref&lt;Velocity&gt;&gt;())</c>
/// calling a <c>static void Process(Mut&lt;Position&gt; position, Ref&lt;Velocity&gt; velocity)</c>.
/// A single-accessor loop needs no such care. This is a JIT register-allocation effect
/// (a dedicated small stack frame optimizes better than one sharing space with the
/// enclosing loop's other locals), not specific to this type — the same applies to any
/// loop touching two or more of this codebase's ref-struct accessors.</para>
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
    /// component column -- typically <see cref="Mut{T}"/> or <see cref="Ref{T}"/>. The
    /// archetype must actually store <typeparamref name="TAccessor"/>'s component type
    /// (i.e. this chunk's query included it via <see cref="ArchetypeQuery.Access{TAccessor}"/>)
    /// -- calling this for a type the archetype never stores is a caller bug, not a
    /// runtime-checked condition, matching every other chunk-level accessor in this
    /// codebase.
    /// </summary>
    public TAccessor Access<TAccessor>() where TAccessor : struct, IComponentAccessor<TAccessor>, allows ref struct
    {
        var typeIndex = TAccessor.TypeIndex;
        var storage = _archetype.Storages[typeIndex];
        var tracked = _world.IsTracked(typeIndex);
        return TAccessor.CreateChunk(storage.RawItems, storage.RawLastMarkedTick, _world.CurrentTick, 0, _archetype.Count, tracked);
    }
}
