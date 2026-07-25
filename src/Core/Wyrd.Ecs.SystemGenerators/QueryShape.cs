using System.Collections.Immutable;

namespace Wyrd.Ecs.SystemGenerators;

internal enum MarkerKind { Writes, Reads, Has }

internal readonly record struct MarkerElement(MarkerKind Kind, string ComponentTypeName);
internal readonly record struct WithoutElement(string TypeName);
internal readonly record struct AnyElement(string Type0Name, string Type1Name);

/// <summary>
/// A query shape extracted from a chain's resolved <c>Query&lt;TShape&gt;</c> receiver
/// type. Deliberately a plain class, not a value-equatable record -- comparing two
/// shapes for the two purposes this design actually needs (exact-overload
/// deduplication via <see cref="ExactShapeTypeName"/>, logical-shape backend sharing
/// via <see cref="QueryShapeExtensions.DedupKey"/>) always goes through those two
/// plain-string members explicitly, never through this type's own equality -- avoiding
/// the well-known pitfall where a record's synthesized equality on
/// <see cref="ImmutableArray{T}"/> fields compares by reference, not element-wise.
/// </summary>
internal sealed class QueryShape
{
    public required string ExactShapeTypeName { get; init; }
    public required ImmutableArray<MarkerElement> Markers { get; init; }
    public required ImmutableArray<WithoutElement> Withouts { get; init; }
    public required ImmutableArray<AnyElement> Anys { get; init; }
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
