using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs;

/// <summary>
/// Hidden-chunk convenience query, tracked/mutable: a <c>foreach</c>-able sequence of
/// direct references to entities' <typeparamref name="T"/> component, walking
/// archetypes internally so no chunk or archetype vocabulary is required. Returned by
/// <see cref="IWorld.QueryMut{T}"/>. Must be consumed with
/// <c>foreach (ref var x in world.QueryMut&lt;T&gt;())</c> — accessing
/// <see cref="Enumerator.Current"/> marks that entity dirty (matching
/// <see cref="Mut{T}"/>'s indexer semantics: access, not proven write, is the tracked
/// event, since C# cannot distinguish the two at this call site). A build-time
/// analyzer (WYRD001) enforces the <c>ref</c> binding.
/// </summary>
public readonly ref struct MutEntityQuery<T> where T : struct, IComponent
{
    private readonly Dictionary<ArchetypeSignature, Archetype>.ValueCollection _archetypes;
    private readonly int _typeIndex;

    internal MutEntityQuery(Dictionary<ArchetypeSignature, Archetype>.ValueCollection archetypes, int typeIndex)
    {
        _archetypes = archetypes;
        _typeIndex = typeIndex;
    }

    /// <summary>Returns the enumerator for this query.</summary>
    public Enumerator GetEnumerator() => new(_archetypes, _typeIndex);

    /// <summary>Enumerates one <typeparamref name="T"/> reference per matching entity.</summary>
    public ref struct Enumerator
    {
        private Dictionary<ArchetypeSignature, Archetype>.ValueCollection.Enumerator _archetypes;
        private readonly int _typeIndex;
        private Span<T> _items;
        private Span<bool> _dirty;
        private int _row;
        private int _count;

        internal Enumerator(Dictionary<ArchetypeSignature, Archetype>.ValueCollection archetypes, int typeIndex)
        {
            _archetypes = archetypes.GetEnumerator();
            _typeIndex = typeIndex;
            _items = default;
            _dirty = default;
            _row = -1;
            _count = 0;
        }

        /// <summary>Marks the current entity dirty, then returns a mutable reference to its component.</summary>
        public ref T Current
        {
            get
            {
                _dirty[_row] = true;
                return ref _items[_row];
            }
        }

        /// <summary>Advances to the next matching entity.</summary>
        public bool MoveNext()
        {
            _row++;
            while (_row >= _count)
            {
                if (!_archetypes.MoveNext()) return false;

                var archetype = _archetypes.Current;
                if (archetype.Count == 0 || !archetype.Signature.Contains(_typeIndex))
                {
                    _count = 0;
                    _row = 0;
                    continue;
                }

                var storage = archetype.Storages[_typeIndex];
                _items = ((T[])storage.RawItems).AsSpan(0, archetype.Count);
                _dirty = storage.RawDirty.AsSpan(0, archetype.Count);
                _count = archetype.Count;
                _row = 0;
            }

            return true;
        }
    }
}
