using System.Collections.Immutable;

namespace Wyrd.Ecs.Generators;

internal enum MarkerKind { Writes, Reads, Has }

internal readonly record struct MarkerElement(MarkerKind Kind, string ComponentTypeName);
internal readonly record struct WithoutElement(string TypeName);
internal readonly record struct AnyElement(ImmutableArray<string> TypeNames)
{
    public bool Equals(AnyElement other) => TypeNames.SequenceEqual(other.TypeNames);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            foreach (var name in TypeNames) hash = hash * 31 + name.GetHashCode();
            return hash;
        }
    }
}

/// <summary>
/// A query shape extracted from a chain's resolved <c>Query&lt;TShape&gt;</c> receiver
/// type. A plain class, not a record -- a record's synthesized equality on
/// <see cref="ImmutableArray{T}"/> fields compares by reference, not element-wise, which
/// is never what two independently-extracted shapes with the same content need. Instead
/// <see cref="IEquatable{QueryShape}"/> is implemented explicitly below with
/// <c>SequenceEqual</c> on each array. Correct equality here matters beyond this type's
/// own two comparison purposes (exact-overload deduplication via
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
    public required ImmutableArray<WithoutElement> Withouts { get; init; }
    public required ImmutableArray<AnyElement> Anys { get; init; }

    public bool Equals(QueryShape? other) =>
        other is not null
        && ExactShapeTypeName == other.ExactShapeTypeName
        && Markers.SequenceEqual(other.Markers)
        && PendingDataElements.SequenceEqual(other.PendingDataElements)
        && Withouts.SequenceEqual(other.Withouts)
        && Anys.SequenceEqual(other.Anys);

    public override bool Equals(object? obj) => obj is QueryShape other && Equals(other);

    public override int GetHashCode() =>
        StableHashCode.Start(ExactShapeTypeName).AddEach(Markers).AddEach(PendingDataElements).AddEach(Withouts).AddEach(Anys);
}

internal static class QueryShapeExtensions
{
    /// <summary>Writes/Reads elements only (Has is filter-only), sorted by component type name -- the canonical order the shared backend (<see cref="QueryChainEmitter.RenderBackend"/>) uses internally so shapes with the same components in a different declaration order still share one backend. Not used for any caller-facing parameter list -- see <see cref="OwnDataElements"/> for that.</summary>
    internal static ImmutableArray<MarkerElement> DataElements(this QueryShape shape) =>
        shape.Markers
            .Where(m => m.Kind != MarkerKind.Has)
            .OrderBy(m => m.ComponentTypeName, StringComparer.Ordinal)
            .ToImmutableArray();

    /// <summary>
    /// Writes/Reads elements only (Has is filter-only), in the order the caller actually wrote
    /// their `.With&lt;&gt;()` calls -- the order their `.ForEach(...)` lambda must use for this
    /// shape. <see cref="QueryShape.Markers"/> is populated outer-tuple-first while walking the
    /// resolved type (see <c>ChainWalker.TryExtractShapeFromQueryType</c>), which is the
    /// *reverse* of declaration order (`.With&lt;A&gt;().With&lt;B&gt;()` produces `(B, (A, Nil))`,
    /// visited B-then-A) -- reversed back here. Every caller-facing delegate/parameter list
    /// (<see cref="QueryChainEmitter.RenderForEachOverload"/> and its Predicate/Parallel
    /// counterparts) is built from this, not <see cref="DataElements"/> -- callers should never
    /// need to know or match the shared backend's alphabetical order.
    /// </summary>
    internal static ImmutableArray<MarkerElement> OwnDataElements(this QueryShape shape) =>
        shape.Markers.Where(m => m.Kind != MarkerKind.Has).Reverse().ToImmutableArray();

    /// <summary>Order-independent identity for deduplication: two shapes with the same elements in different declaration order produce the same key.</summary>
    internal static string DedupKey(this QueryShape shape)
    {
        var markers = shape.Markers.OrderBy(m => m.ComponentTypeName, StringComparer.Ordinal).Select(m => $"{m.Kind}:{m.ComponentTypeName}");
        var withouts = shape.Withouts.OrderBy(w => w.TypeName, StringComparer.Ordinal).Select(w => $"X:{w.TypeName}");
        var anys = shape.Anys
            .Select(a => $"A:{string.Join(",", a.TypeNames.OrderBy(t => t, StringComparer.Ordinal))}")
            .OrderBy(s => s, StringComparer.Ordinal);
        return string.Join("|", markers.Concat(withouts).Concat(anys));
    }

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
