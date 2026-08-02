namespace Wyrd.Ecs.Internal;

/// <summary>
/// Component storage for a single archetype, indexed directly by <see cref="TypeIndex{T}"/>
/// value (a small int) rather than hashed, since every lookup here is on the query and
/// structural-move hot path.
/// </summary>
internal sealed class ArchetypeStorages
{
    private IComponentStorage?[] _storages = [];

    internal IComponentStorage this[int typeIndex]
    {
        get => _storages[typeIndex]!;
        set
        {
            EnsureCapacity(typeIndex + 1);
            _storages[typeIndex] = value;
        }
    }

    internal bool TryGetValue(int typeIndex, out IComponentStorage storage)
    {
        if (typeIndex < _storages.Length && _storages[typeIndex] is { } existing)
        {
            storage = existing;
            return true;
        }

        storage = null!;
        return false;
    }

    internal ValuesEnumerable Values => new(this);

    public Enumerator GetEnumerator() => new(_storages);

    private void EnsureCapacity(int capacity) => ArrayGrowth.EnsureCapacity(ref _storages, capacity);

    /// <summary>Enumerates every occupied slot as <c>(TypeIndex, Storage)</c>, skipping empty ones.</summary>
    internal struct Enumerator
    {
        private readonly IComponentStorage?[] _storages;
        private int _index;

        internal Enumerator(IComponentStorage?[] storages)
        {
            _storages = storages;
            _index = -1;
        }

        public KeyValuePair<int, IComponentStorage> Current => new(_index, _storages[_index]!);

        public bool MoveNext()
        {
            while (++_index < _storages.Length)
            {
                if (_storages[_index] is not null) return true;
            }
            return false;
        }
    }

    /// <summary>Enumerates every occupied slot's storage, skipping empty ones.</summary>
    internal readonly struct ValuesEnumerable
    {
        private readonly ArchetypeStorages _map;
        internal ValuesEnumerable(ArchetypeStorages map) => _map = map;
        public ValuesEnumerator GetEnumerator() => new(_map._storages);
    }

    internal struct ValuesEnumerator
    {
        private readonly IComponentStorage?[] _storages;
        private int _index;

        internal ValuesEnumerator(IComponentStorage?[] storages)
        {
            _storages = storages;
            _index = -1;
        }

        public IComponentStorage Current => _storages[_index]!;

        public bool MoveNext()
        {
            while (++_index < _storages.Length)
            {
                if (_storages[_index] is not null) return true;
            }
            return false;
        }
    }
}
