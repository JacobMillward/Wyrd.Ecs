namespace Wyrd.Ecs.Persistence;

/// <summary>
/// Where persisted bytes physically live — the storage-backend seam. A
/// local-filesystem implementation (<see cref="FileStore"/>) is the
/// only one built so far; a future backend (e.g. SQLite) implements this interface
/// without the rest of the pipeline changing.
/// </summary>
public interface IPersistenceStore
{
    /// <summary>Opens a stream to write a fresh full checkpoint to. Overwrites any existing checkpoint.</summary>
    Stream OpenCheckpointWrite();

    /// <summary>Opens a stream to read the current checkpoint from.</summary>
    Stream OpenCheckpointRead();
}
