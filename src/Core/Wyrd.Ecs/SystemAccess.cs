namespace Wyrd.Ecs;

/// <summary>
/// One declared system's component access footprint: every type it reads, every type
/// it writes, derived directly from its query shape's <c>Reads&lt;T&gt;</c>/<c>Writes&lt;T&gt;</c>
/// elements. <c>Has</c>/<c>Without</c>/<c>Any</c> elements never appear here since they're
/// filter-only, never a data dependency. Populated by the query-chain generator's
/// <c>GeneratedSystemAccess.Entries</c> registry, consumed by
/// <see cref="Internal.StagePlanner"/>'s conflict-graph construction.
/// </summary>
public sealed class SystemAccess : IEquatable<SystemAccess>
{
    /// <summary>Every component type this system reads.</summary>
    public IReadOnlyList<Type> Reads { get; }

    /// <summary>Every component type this system writes.</summary>
    public IReadOnlyList<Type> Writes { get; }

    /// <summary>Wraps an already-computed read/write set, populated by the query-chain generator's <c>GeneratedSystemAccess</c> registry.</summary>
    public SystemAccess(IReadOnlyList<Type> Reads, IReadOnlyList<Type> Writes)
    {
        this.Reads = Reads;
        this.Writes = Writes;
    }

    /// <summary>Value-equality: the same reads and the same writes, in the same order.</summary>
    public bool Equals(SystemAccess? other) =>
        other is not null && Reads.SequenceEqual(other.Reads) && Writes.SequenceEqual(other.Writes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as SystemAccess);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var type in Reads) hash.Add(type);
        foreach (var type in Writes) hash.Add(type);
        return hash.ToHashCode();
    }
}
