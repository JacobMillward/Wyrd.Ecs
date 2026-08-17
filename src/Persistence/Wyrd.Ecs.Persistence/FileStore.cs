namespace Wyrd.Ecs.Persistence;

/// <summary>
/// Stores the checkpoint as a single file on disk at <paramref name="path"/>. Writes
/// go to a temporary file first and are only moved into place once the save completes
/// successfully, so a crash or exception partway through a save can't corrupt or
/// truncate the existing save file. Equality is by <see cref="Path"/>, so a
/// <c>Save(path)</c>/<c>Load(path)</c> call's freshly-constructed <see cref="FileStore"/>
/// is recognized as the same store <c>WorldBuilder.SetPersistence</c> paired a registry
/// with at the same path, letting it resolve that registry.
/// </summary>
public sealed class FileStore(string path) : IPersistenceStore, IEquatable<FileStore>
{
    /// <summary>The path this store reads/writes its checkpoint at.</summary>
    public string Path { get; } = path;

    /// <inheritdoc/>
    public Stream OpenCheckpointWrite() => new Internal.AtomicFileWriteStream(Path);

    /// <inheritdoc/>
    public Stream OpenCheckpointRead() => File.OpenRead(Path);

    /// <inheritdoc/>
    public bool Equals(FileStore? other) => other is not null && Path == other.Path;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as FileStore);

    /// <inheritdoc/>
    public override int GetHashCode() => Path.GetHashCode();
}
