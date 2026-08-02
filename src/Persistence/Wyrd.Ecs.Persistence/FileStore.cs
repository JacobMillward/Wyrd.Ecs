namespace Wyrd.Ecs.Persistence;

/// <summary>
/// Stores the checkpoint as a single file on disk at <paramref name="path"/>. Writes
/// go to a temporary file first and are only moved into place once the save completes
/// successfully, so a crash or exception partway through a save can't corrupt or
/// truncate the existing save file.
/// </summary>
public sealed class FileStore(string path) : IPersistenceStore
{
    /// <summary>The path this store reads/writes its checkpoint at.</summary>
    public string Path { get; } = path;

    /// <inheritdoc/>
    public Stream OpenCheckpointWrite() => new Internal.AtomicFileWriteStream(Path);

    /// <inheritdoc/>
    public Stream OpenCheckpointRead() => File.OpenRead(Path);
}
