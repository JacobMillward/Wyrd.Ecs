using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs;

/// <summary>
/// Non-destructive, cursor-based read over one component type's change log across
/// every archetype that has it. Obtained from <see cref="ChangeConsumer{T}.ReadChanges"/>.
/// Reading never clears or reorders the log, so any number of independent consumers,
/// each with their own cursor, observe the same underlying entries without affecting
/// each other. See the design's Streaming the change log to multiple independent
/// consumers section.
/// </summary>
public readonly ref struct ChangeReadQuery<T> where T : struct, IComponent
{
    private readonly Dictionary<ArchetypeSignature, Archetype>.ValueCollection _archetypes;
    private readonly int _typeIndex;
    private readonly int _sinceTick;

    internal ChangeReadQuery(Dictionary<ArchetypeSignature, Archetype>.ValueCollection archetypes, int typeIndex, int sinceTick)
    {
        _archetypes = archetypes;
        _typeIndex = typeIndex;
        _sinceTick = sinceTick;
    }

    /// <summary>Returns the enumerator for this read.</summary>
    public Enumerator GetEnumerator() => new(_archetypes, _typeIndex, _sinceTick);

    /// <summary>Enumerates every <see cref="DirtyEntry"/> recorded after the cursor, across every matching archetype.</summary>
    public ref struct Enumerator
    {
        private Dictionary<ArchetypeSignature, Archetype>.ValueCollection.Enumerator _archetypes;
        private readonly int _typeIndex;
        private readonly int _sinceTick;
        private ReadOnlySpan<DirtyEntry> _entries;
        private int _index;

        internal Enumerator(Dictionary<ArchetypeSignature, Archetype>.ValueCollection archetypes, int typeIndex, int sinceTick)
        {
            _archetypes = archetypes.GetEnumerator();
            _typeIndex = typeIndex;
            _sinceTick = sinceTick;
            _entries = default;
            _index = -1;
        }

        /// <summary>The current change-log entry.</summary>
        public DirtyEntry Current => _entries[_index];

        /// <summary>Advances to the next entry.</summary>
        public bool MoveNext()
        {
            _index++;
            while (_index >= _entries.Length)
            {
                if (!_archetypes.MoveNext()) return false;

                var archetype = _archetypes.Current;
                if (!archetype.Signature.Contains(_typeIndex))
                {
                    _entries = default;
                    _index = 0;
                    continue;
                }

                var storage = archetype.Storages[_typeIndex];
                _entries = storage.ReadDirtyLogSince(_sinceTick);
                _index = 0;
            }

            return true;
        }
    }
}
