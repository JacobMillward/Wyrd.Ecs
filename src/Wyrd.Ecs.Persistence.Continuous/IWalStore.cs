namespace Wyrd.Ecs.Persistence.Continuous;

/// <summary>
/// Where WAL segments physically live — deliberately not an extension of
/// <c>Wyrd.Ecs.Persistence.IPersistenceStore</c>. A checkpoint is one coherent
/// snapshot, correctly written via an atomic swap; a WAL segment is a stream that
/// stays open for an entire session, accepting durable incremental appends, where a
/// crash must preserve everything flushed so far rather than roll back to nothing —
/// different enough storage shapes that one type shouldn't serve both. It's also not
/// a capability probed for via <c>is</c> the way <c>ITransactionalWriteStream</c> is:
/// the natural implementer of a WAL capability, <c>FileStore</c>, lives in
/// <c>Wyrd.Ecs.Persistence</c>, a package this one depends on, so an interface defined
/// here couldn't be implemented there without inverting the reference direction. A
/// consumer wanting continuous persistence configures a separate <see cref="IWalStore"/>
/// alongside their <c>IPersistenceStore</c>, not one object doing both jobs. Segments
/// are identified by the tick they start at.
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
}
