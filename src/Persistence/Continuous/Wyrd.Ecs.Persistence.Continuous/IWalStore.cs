namespace Wyrd.Ecs.Persistence.Continuous;

/// <summary>
/// Where WAL segments physically live, separate from <c>IPersistenceStore</c>: a
/// checkpoint is one snapshot written and swapped in atomically, while a WAL segment
/// stays open for a session, taking small incremental writes so recent changes survive
/// a crash between checkpoints. Segments are identified by the tick they start at.
/// </summary>
public interface IWalStore
{
    /// <summary>Creates a brand-new segment starting at <paramref name="startTick"/> and opens it for writing. Throws if a segment already exists for this tick.</summary>
    Stream OpenSegmentAppend(int startTick);

    /// <summary>Opens an existing segment for reading, from the start.</summary>
    Stream OpenSegmentRead(int startTick);

    /// <summary>Every segment's starting tick currently present, in ascending order.</summary>
    IReadOnlyList<int> ListSegmentStartTicks();

    /// <summary>Deletes the segment starting at <paramref name="startTick"/>.</summary>
    void DeleteSegment(int startTick);

    /// <summary>
    /// Flushes <paramref name="segment"/> all the way to disk, not just in-memory buffers,
    /// so a crash right after this call can't lose what was just written. Must be a stream
    /// this store returned from <see cref="OpenSegmentAppend"/>.
    /// </summary>
    void Flush(Stream segment);
}
