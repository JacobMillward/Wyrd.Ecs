using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs;

/// <summary>
/// The archetypes matching one <see cref="ArchetypeQuery.Resolve"/> call, each wrapped as
/// an <see cref="ArchetypeChunk"/>. A plain struct enumerator (no <c>IEnumerable&lt;T&gt;</c>),
/// so `foreach` over this never pays for interface dispatch or boxing.
/// </summary>
public readonly struct ArchetypeChunks
{
    private readonly Archetype[] _archetypes;
    private readonly World _world;

    internal ArchetypeChunks(Archetype[] archetypes, World world)
    {
        _archetypes = archetypes;
        _world = world;
    }

    /// <summary>The number of matching archetypes.</summary>
    public int Count => _archetypes.Length;

    /// <summary>The chunk for the matching archetype at <paramref name="index"/>.</summary>
    public ArchetypeChunk this[int index] => new(_archetypes[index], _world);

    /// <summary>Returns the enumerator for this sequence of chunks.</summary>
    public Enumerator GetEnumerator() => new(this);

    /// <summary>Enumerates one <see cref="ArchetypeChunk"/> per matching archetype.</summary>
    public struct Enumerator
    {
        private readonly ArchetypeChunks _chunks;
        private int _index;

        internal Enumerator(ArchetypeChunks chunks)
        {
            _chunks = chunks;
            _index = -1;
        }

        /// <summary>The current chunk.</summary>
        public ArchetypeChunk Current => _chunks[_index];

        /// <summary>Advances to the next matching archetype with at least one entity, skipping empty ones.</summary>
        public bool MoveNext()
        {
            do
            {
                _index++;
            } while (_index < _chunks.Count && _chunks[_index].Count == 0);

            return _index < _chunks.Count;
        }
    }
}
