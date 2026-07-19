namespace Wyrd.Ecs.Persistence;

/// <summary>
/// Stores the checkpoint as a single file on the local filesystem at
/// <paramref name="path"/>.
/// </summary>
public sealed class FileStore(string path) : IPersistenceStore
{
    /// <inheritdoc/>
    public Stream OpenCheckpointWrite() => File.Create(path);

    /// <inheritdoc/>
    public Stream OpenCheckpointRead() => File.OpenRead(path);
}
