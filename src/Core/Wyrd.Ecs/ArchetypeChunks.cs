using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs;

/// <summary>
/// The archetypes matching one <see cref="ArchetypeQuery.Resolve(World)"/> call, each wrapped as
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

    /// <summary>
    /// Row count at or below which an archetype dispatches as a single chunk. Above it,
    /// <see cref="CollectParallelChunks"/> splits the archetype into fixed-size row ranges so
    /// one huge archetype still spreads across worker threads; per-slice accessor setup is
    /// negligible next to this many rows of per-row work.
    /// </summary>
    internal const int ParallelSliceRows = 4096;

    /// <summary>The chunk for the matching archetype at <paramref name="index"/>.</summary>
    public ArchetypeChunk this[int index] => new(_archetypes[index], _world);

    /// <summary>Returns the enumerator for this sequence of chunks.</summary>
    public Enumerator GetEnumerator() => new(this);

    /// <summary>
    /// Appends the chunks a parallel dispatch should process to <paramref name="output"/>:
    /// one chunk per non-empty archetype, except archetypes holding more than
    /// <see cref="ParallelSliceRows"/> rows, which expand into consecutive fixed-size ranges.
    /// The ranges partition each archetype's rows exactly once and every chunk view is
    /// range-relative, so per-row work matches sequential iteration's. Public because
    /// generated code compiles into arbitrary consumer assemblies with no
    /// <c>InternalsVisibleTo</c> grant - not intended for hand-written call sites, which
    /// should keep enumerating via <see cref="GetEnumerator"/>.
    /// </summary>
    public void CollectParallelChunks(List<ArchetypeChunk> output)
    {
        for (var i = 0; i < _archetypes.Length; i++)
        {
            var archetype = _archetypes[i];
            var remaining = archetype.Count;
            if (remaining == 0) continue;

            var start = 0;
            do
            {
                var count = remaining < ParallelSliceRows ? remaining : ParallelSliceRows;
                output.Add(new ArchetypeChunk(archetype, _world, start, count));
                start += count;
                remaining -= count;
            } while (remaining > 0);
        }
    }

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
