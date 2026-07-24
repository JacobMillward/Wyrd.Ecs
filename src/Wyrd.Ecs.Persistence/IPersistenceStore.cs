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

    /// <summary>
    /// Opens a stream to read the current checkpoint from. Must throw
    /// <see cref="FileNotFoundException"/> (or a subclass) when no checkpoint has ever
    /// been written yet — the continuous-persistence package's checkpoint builder relies
    /// on exactly this to tell "empty store" apart from a real read failure when merging
    /// a WAL into a checkpoint for the first time. <see cref="FileStore"/> gets this for
    /// free from <see cref="File.OpenRead(string)"/>; a non-file-backed implementation
    /// must throw it explicitly for the same case.
    /// </summary>
    Stream OpenCheckpointRead();
}
