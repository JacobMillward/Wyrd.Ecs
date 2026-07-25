namespace Wyrd.Ecs;

/// <summary>
/// One declared system's component access footprint — every type it reads, every type
/// it writes, derived directly from its query shape's <c>Reads&lt;T&gt;</c>/<c>Writes&lt;T&gt;</c>
/// elements. <c>Has</c>/<c>Without</c>/<c>Any</c> elements never appear here — they're
/// filter-only, never a data dependency. Populated by the query-chain generator's
/// <c>GeneratedSystemAccess.Entries</c> registry (Task 9), consumed by the
/// static-parallel-scheduler plan's (not yet built) conflict-graph construction.
/// </summary>
public sealed class SystemAccess : IEquatable<SystemAccess>
{
    public IReadOnlyList<Type> Reads { get; }
    public IReadOnlyList<Type> Writes { get; }

    public SystemAccess(IReadOnlyList<Type> Reads, IReadOnlyList<Type> Writes)
    {
        this.Reads = Reads;
        this.Writes = Writes;
    }

    public bool Equals(SystemAccess? other) =>
        other is not null && Reads.SequenceEqual(other.Reads) && Writes.SequenceEqual(other.Writes);

    public override bool Equals(object? obj) => Equals(obj as SystemAccess);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var type in Reads) hash.Add(type);
        foreach (var type in Writes) hash.Add(type);
        return hash.ToHashCode();
    }
}
