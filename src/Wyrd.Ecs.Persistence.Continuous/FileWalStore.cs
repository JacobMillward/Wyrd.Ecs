namespace Wyrd.Ecs.Persistence.Continuous;

/// <summary>
/// Stores WAL segments as separate files on the local filesystem, named
/// <c>{basePath}.wal.{startTick}</c>. A fresh segment is always a brand-new file —
/// <see cref="OpenSegmentAppend"/> throws if one already exists for a given tick. This
/// store never resumes appending to a segment left over from a previous process run;
/// a new run always starts a fresh segment, and whatever's left in an old one is read
/// once during recovery, not appended to further.
/// </summary>
public sealed class FileWalStore(string basePath) : IWalStore
{
    /// <inheritdoc/>
    public Stream OpenSegmentAppend(int startTick) =>
        new FileStream(SegmentPath(startTick), FileMode.CreateNew, FileAccess.Write, FileShare.Read);

    /// <inheritdoc/>
    public Stream OpenSegmentRead(int startTick) => File.OpenRead(SegmentPath(startTick));

    /// <inheritdoc/>
    public IReadOnlyList<int> ListSegmentStartTicks()
    {
        var directory = Path.GetDirectoryName(basePath);
        if (string.IsNullOrEmpty(directory)) directory = ".";
        if (!Directory.Exists(directory)) return [];

        var prefix = Path.GetFileName(basePath) + ".wal.";
        var ticks = new List<int>();
        foreach (var file in Directory.EnumerateFiles(directory, prefix + "*"))
        {
            var name = Path.GetFileName(file);
            if (name.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(name.AsSpan(prefix.Length), out var tick))
                ticks.Add(tick);
        }

        ticks.Sort();
        return ticks;
    }

    /// <inheritdoc/>
    public void DeleteSegment(int startTick) => File.Delete(SegmentPath(startTick));

    private string SegmentPath(int startTick) => $"{basePath}.wal.{startTick}";
}
