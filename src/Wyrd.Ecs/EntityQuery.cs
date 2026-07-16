using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs;

/// <summary>
/// The hidden-chunk convenience tier: a <c>foreach</c>-able sequence of one component
/// accessor per matching entity, walking archetypes internally so no chunk or
/// archetype vocabulary is required to write a query. Returned by
/// <see cref="IWorld.Query{TAccess0}()"/>. Each yielded <typeparamref name="TAccess0"/>
/// has <c>Length == 1</c> — index it at <c>[0]</c> (the design spec's zero-index
/// illustrative example doesn't compile as written against the committed
/// <see cref="IWorld"/> contract, since <see cref="Enumerator.Current"/> returns
/// <typeparamref name="TAccess0"/> itself, a chunk-shaped accessor).
/// </summary>
public readonly ref struct EntityQuery<TAccess0> where TAccess0 : struct, IComponentAccessor<TAccess0>, allows ref struct
{
    private readonly Dictionary<ArchetypeSignature, Archetype>.ValueCollection _archetypes;
    private readonly int _typeIndex;

    internal EntityQuery(Dictionary<ArchetypeSignature, Archetype>.ValueCollection archetypes, int typeIndex)
    {
        _archetypes = archetypes;
        _typeIndex = typeIndex;
    }

    /// <summary>Returns the enumerator for this query.</summary>
    public Enumerator GetEnumerator() => new(_archetypes, _typeIndex);

    /// <summary>Enumerates one <typeparamref name="TAccess0"/> accessor per matching entity.</summary>
    public ref struct Enumerator
    {
        private Dictionary<ArchetypeSignature, Archetype>.ValueCollection.Enumerator _archetypes;
        private readonly int _typeIndex;
        private IComponentStorage? _storage;
        private int _row;
        private int _count;

        internal Enumerator(Dictionary<ArchetypeSignature, Archetype>.ValueCollection archetypes, int typeIndex)
        {
            _archetypes = archetypes.GetEnumerator();
            _typeIndex = typeIndex;
            _storage = null;
            _row = -1;
            _count = 0;
        }

        /// <summary>The current entity's component accessor (<c>Length == 1</c>).</summary>
        public TAccess0 Current => TAccess0.CreateChunk(_storage!.RawItems, _storage.RawDirty, _row, 1);

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

                _storage = archetype.Storages[_typeIndex];
                _count = archetype.Count;
                _row = 0;
            }

            return true;
        }
    }
}
