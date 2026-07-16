using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs;

/// <summary>
/// Unified entity-tier query, replacing <c>QueryMut&lt;T&gt;</c>/<c>QueryRef&lt;T&gt;</c>
/// outright: a <c>foreach</c>-able sequence of <see cref="QueryRow{T0}"/>, one per
/// matching entity, walking archetypes internally so no chunk or archetype
/// vocabulary is required. Returned by <see cref="IWorld"/>'s <c>Query&lt;T0&gt;()</c>.
/// There is no separate tracked/untracked overload here — <see cref="QueryRow{T0}.Get{T}"/>
/// decides per call, see the design's Mutation and read ergonomics section.
/// </summary>
public readonly ref struct Query<T0> where T0 : struct, IComponent
{
    private readonly Dictionary<ArchetypeSignature, Archetype>.ValueCollection _archetypes;
    private readonly int _typeIndex0;
    private readonly int _tick;

    internal Query(Dictionary<ArchetypeSignature, Archetype>.ValueCollection archetypes, int typeIndex0, int tick)
    {
        _archetypes = archetypes;
        _typeIndex0 = typeIndex0;
        _tick = tick;
    }

    /// <summary>Returns the enumerator for this query.</summary>
    public Enumerator GetEnumerator() => new(_archetypes, _typeIndex0, _tick);

    /// <summary>Enumerates one <see cref="QueryRow{T0}"/> per matching entity.</summary>
    public ref struct Enumerator
    {
        private Dictionary<ArchetypeSignature, Archetype>.ValueCollection.Enumerator _archetypes;
        private readonly int _typeIndex0;
        private readonly int _tick;
        private Span<T0> _items0;
        private Span<int> _lastMarkedTick0;
        private DirtyLog _dirtyLog0;
        private Entity[] _entities;
        private int _row;
        private int _count;

        internal Enumerator(Dictionary<ArchetypeSignature, Archetype>.ValueCollection archetypes, int typeIndex0, int tick)
        {
            _archetypes = archetypes.GetEnumerator();
            _typeIndex0 = typeIndex0;
            _tick = tick;
            _items0 = default;
            _lastMarkedTick0 = default;
            _dirtyLog0 = null!;
            _entities = Array.Empty<Entity>();
            _row = -1;
            _count = 0;
        }

        /// <summary>The current row.</summary>
        public QueryRow<T0> Current => new(_items0, _lastMarkedTick0, _dirtyLog0, _tick, _row, _entities[_row]);

        /// <summary>Advances to the next matching entity, caching a new archetype's storage exactly once per transition.</summary>
        public bool MoveNext()
        {
            _row++;
            while (_row >= _count)
            {
                if (!_archetypes.MoveNext()) return false;

                var archetype = _archetypes.Current;
                if (archetype.Count == 0 || !archetype.Signature.Contains(_typeIndex0))
                {
                    _count = 0;
                    _row = 0;
                    continue;
                }

                var storage0 = archetype.Storages[_typeIndex0];
                _items0 = ((T0[])storage0.RawItems).AsSpan(0, archetype.Count);
                _lastMarkedTick0 = storage0.RawLastMarkedTick.AsSpan(0, archetype.Count);
                _dirtyLog0 = storage0.GetDirtyLogForChunk(archetype.Entities, archetype.Count);
                _entities = archetype.Entities;
                _count = archetype.Count;
                _row = 0;
            }

            return true;
        }
    }
}
