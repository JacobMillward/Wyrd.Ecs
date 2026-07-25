using System.Collections.Immutable;

namespace Wyrd.Ecs.SystemGenerators;

internal enum MarkerKind { Writes, Reads, Has }

internal readonly record struct MarkerElement(MarkerKind Kind, string ComponentTypeName);
internal readonly record struct WithoutElement(string TypeName);
internal readonly record struct AnyElement(string Type0Name, string Type1Name);

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
    public required ImmutableArray<WithoutElement> Withouts { get; init; }
    public required ImmutableArray<AnyElement> Anys { get; init; }

    public bool Equals(QueryShape? other) =>
        other is not null
        && ExactShapeTypeName == other.ExactShapeTypeName
        && Markers.SequenceEqual(other.Markers)
        && Withouts.SequenceEqual(other.Withouts)
        && Anys.SequenceEqual(other.Anys);

    public override bool Equals(object? obj) => obj is QueryShape other && Equals(other);

    /// <summary>Manual combine, not <c>System.HashCode</c> -- this project targets netstandard2.0, where that type doesn't exist.</summary>
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = ExactShapeTypeName.GetHashCode();
            foreach (var marker in Markers) hash = hash * 31 + marker.GetHashCode();
            foreach (var without in Withouts) hash = hash * 31 + without.GetHashCode();
            foreach (var any in Anys) hash = hash * 31 + any.GetHashCode();
            return hash;
        }
    }
}

internal static class QueryShapeExtensions
{
    /// <summary>Writes/Reads elements only (Has is filter-only), sorted by component type name -- the canonical order used for every generated parameter list.</summary>
    internal static ImmutableArray<MarkerElement> DataElements(this QueryShape shape) =>
        shape.Markers
            .Where(m => m.Kind != MarkerKind.Has)
            .OrderBy(m => m.ComponentTypeName, StringComparer.Ordinal)
            .ToImmutableArray();

    /// <summary>Order-independent identity for deduplication: two shapes with the same elements in different declaration order produce the same key.</summary>
    internal static string DedupKey(this QueryShape shape)
    {
        var markers = shape.Markers.OrderBy(m => m.ComponentTypeName, StringComparer.Ordinal).Select(m => $"{m.Kind}:{m.ComponentTypeName}");
        var withouts = shape.Withouts.OrderBy(w => w.TypeName, StringComparer.Ordinal).Select(w => $"X:{w.TypeName}");
        var anys = shape.Anys
            .Select(a => $"A:{string.Join(",", new[] { a.Type0Name, a.Type1Name }.OrderBy(t => t, StringComparer.Ordinal))}")
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
