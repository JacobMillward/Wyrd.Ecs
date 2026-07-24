namespace Wyrd.Ecs.Persistence;

/// <summary>
/// Stores the checkpoint as a single file on the local filesystem at
/// <paramref name="path"/>. <see cref="OpenCheckpointWrite"/> writes go to a temporary
/// sibling file first and are only moved into place when the returned stream is
/// disposed without <see cref="ITransactionalWriteStream.Abort"/> having been called
/// (see <see cref="Internal.AtomicFileWriteStream"/>) — a save that throws partway
/// through leaves the previous checkpoint untouched instead of a truncated file where
/// it used to be.
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
