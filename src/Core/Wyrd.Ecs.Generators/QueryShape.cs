using System.Collections.Immutable;

namespace Wyrd.Ecs.Generators;

internal enum MarkerKind { Writes, Reads }

internal readonly record struct MarkerElement(MarkerKind Kind, string ComponentTypeName);

/// <summary>
/// A query shape extracted from a chain's resolved <c>Query&lt;TShape&gt;</c> receiver
/// type. Only <c>.With&lt;T&gt;()</c> data elements ever appear here — <c>.Without</c>/
/// <c>.Has</c>/<c>.Any</c> apply to <c>Query&lt;TShape&gt;.Filter</c> at runtime instead
/// (see <c>Query.cs</c>), never touching <typeparamref name="TShape"/> at all, so there is
/// nothing about them for this type to carry anymore. A plain class, not a record -- a
/// record's synthesized equality on <see cref="ImmutableArray{T}"/> fields compares by
/// reference, not element-wise, which is never what two independently-extracted shapes with
/// the same content need. Instead <see cref="IEquatable{QueryShape}"/> is implemented
/// explicitly below with <c>SequenceEqual</c>. Correct equality here matters beyond this
/// type's own two comparison purposes (exact-overload deduplication via
/// <see cref="ExactShapeTypeName"/>, logical-shape backend sharing via
/// <see cref="QueryShapeExtensions.DedupKey"/>): <c>QueryChainGenerator</c>'s
/// <see cref="IIncrementalGenerator"/> pipeline threads <see cref="QueryShape"/> values
/// through <c>Select</c>/<c>Where</c>/<c>Collect</c>/<c>Combine</c>, and Roslyn's
/// incremental caching relies on value equality between runs to decide whether
/// <c>RegisterSourceOutput</c> needs to re-run at all -- reference equality here would
/// make every edit anywhere in a consuming project look like a change to every shape,
/// forcing a full regeneration on every keystroke.
/// </summary>
internal sealed class QueryShape : IEquatable<QueryShape>
{
    public required string ExactShapeTypeName { get; init; }
    public required ImmutableArray<MarkerElement> Markers { get; init; }
    public required ImmutableArray<string> PendingDataElements { get; init; }

    public bool Equals(QueryShape? other) =>
        other is not null
        && ExactShapeTypeName == other.ExactShapeTypeName
        && Markers.SequenceEqual(other.Markers)
        && PendingDataElements.SequenceEqual(other.PendingDataElements);

    public override bool Equals(object? obj) => obj is QueryShape other && Equals(other);

    public override int GetHashCode() =>
        StableHashCode.Start(ExactShapeTypeName).AddEach(Markers).AddEach(PendingDataElements);
}

internal static class QueryShapeExtensions
{
    /// <summary>Writes/Reads elements, sorted by component type name -- the canonical order the shared backend (<see cref="QueryChainEmitter.RenderBackend"/>) uses internally so shapes with the same components in a different declaration order still share one backend. Not used for any caller-facing parameter list -- see <see cref="OwnDataElements"/> for that.</summary>
    internal static ImmutableArray<MarkerElement> DataElements(this QueryShape shape) =>
        shape.Markers.OrderBy(m => m.ComponentTypeName, StringComparer.Ordinal).ToImmutableArray();

    /// <summary>
    /// Writes/Reads elements, in the order the caller actually wrote their `.With&lt;&gt;()`
    /// calls -- the order their `.ForEach(...)` lambda must use for this shape.
    /// <see cref="QueryShape.Markers"/> is already stored in that declaration order (see
    /// <c>ChainWalker.TryExtractShapeFromQueryType</c>). Every caller-facing
    /// delegate/parameter list (<see cref="QueryChainEmitter.RenderForEachOverload"/> and its
    /// Predicate/Parallel counterparts) is built from this, not <see cref="DataElements"/> --
    /// callers should never need to know or match the shared backend's alphabetical order.
    /// </summary>
    internal static ImmutableArray<MarkerElement> OwnDataElements(this QueryShape shape) => shape.Markers;

    /// <summary>Order-independent identity for deduplication: two shapes with the same elements in different declaration order produce the same key.</summary>
    internal static string DedupKey(this QueryShape shape) =>
        string.Join("|", shape.Markers.OrderBy(m => m.ComponentTypeName, StringComparer.Ordinal).Select(m => $"{m.Kind}:{m.ComponentTypeName}"));

    /// <summary>A short, stable, valid-C#-identifier suffix derived from <see cref="DedupKey"/> -- names the shared backend (ArchetypeQuery instance, delegate types) that two or more shapes with the same DedupKey reuse.</summary>
    internal static string HashName(this QueryShape shape)
    {
        var key = shape.DedupKey();
        var hash = 2166136261u; // FNV-1a -- deterministic across runs, unlike string.GetHashCode(), which is randomized per-process.
        foreach (var c in key)
        {
            hash ^= c;
            hash *= 16777619u;
        }
        return hash.ToString("x8");
    }
}
