namespace Wyrd.Ecs.Persistence;

/// <summary>
/// Where a checkpoint's bytes are physically stored. <see cref="FileStore"/> saves to a
/// local file; implement this interface directly to save somewhere else (a database,
/// cloud storage, etc.) without changing how <c>World.Save</c>/<c>World.Load</c> are used.
/// </summary>
public interface IPersistenceStore
{
    /// <summary>Opens a stream to write a fresh full checkpoint to. Overwrites any existing checkpoint.</summary>
    Stream OpenCheckpointWrite();

    /// <summary>
    /// Opens a stream to read the current checkpoint from. Must throw
    /// <see cref="FileNotFoundException"/> (or a subclass) when no checkpoint has ever
    /// been written, so callers can tell "nothing saved yet" apart from a real read
    /// failure.
    /// </summary>
    Stream OpenCheckpointRead();
}
