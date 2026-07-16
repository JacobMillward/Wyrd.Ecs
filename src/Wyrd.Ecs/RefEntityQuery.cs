using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs;

/// <summary>
/// Hidden-chunk convenience query, read-only: a <c>foreach</c>-able sequence of
/// read-only references to entities' <typeparamref name="T"/> component. Never marks
/// anything dirty. Returned by <see cref="IWorld.QueryRef{T}"/>.
/// </summary>
public readonly ref struct RefEntityQuery<T> where T : struct, IComponent
{
    private readonly Dictionary<ArchetypeSignature, Archetype>.ValueCollection _archetypes;
    private readonly int _typeIndex;

    internal RefEntityQuery(Dictionary<ArchetypeSignature, Archetype>.ValueCollection archetypes, int typeIndex)
    {
        _archetypes = archetypes;
        _typeIndex = typeIndex;
    }

    /// <summary>Returns the enumerator for this query.</summary>
    public Enumerator GetEnumerator() => new(_archetypes, _typeIndex);

    /// <summary>Enumerates one read-only <typeparamref name="T"/> reference per matching entity.</summary>
    public ref struct Enumerator
    {
        private Dictionary<ArchetypeSignature, Archetype>.ValueCollection.Enumerator _archetypes;
        private readonly int _typeIndex;
        private ReadOnlySpan<T> _items;
        private int _row;
        private int _count;

        internal Enumerator(Dictionary<ArchetypeSignature, Archetype>.ValueCollection archetypes, int typeIndex)
        {
            _archetypes = archetypes.GetEnumerator();
            _typeIndex = typeIndex;
            _items = default;
            _row = -1;
            _count = 0;
        }

        /// <summary>A read-only reference to the current entity's component.</summary>
        public ref readonly T Current => ref _items[_row];

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
                _count = archetype.Count;
                _row = 0;
            }

            return true;
        }
    }
}
