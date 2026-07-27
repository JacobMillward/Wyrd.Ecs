namespace Wyrd.Ecs.Generators;

/// <summary>
/// Manual combine, not <c>System.HashCode</c> -- this project targets netstandard2.0,
/// where that type doesn't exist. Order-sensitive, matching the order-sensitive
/// <c>SequenceEqual</c> comparisons in each caller's own <c>Equals</c> override -- values
/// needing order-independent identity (see <see cref="QueryShapeExtensions.DedupKey"/>,
/// which sorts into a string key and hashes that separately) don't go through this type.
/// </summary>
internal readonly struct StableHashCode
{
    private readonly int _value;

    private StableHashCode(int value) => _value = value;

    internal static StableHashCode Start<T>(T seed) => new(seed?.GetHashCode() ?? 0);

    internal StableHashCode Add<T>(T value) => new(unchecked(_value * 31 + (value?.GetHashCode() ?? 0)));

    internal StableHashCode AddEach<T>(IEnumerable<T> values)
    {
        var result = this;
        foreach (var value in values) result = result.Add(value);
        return result;
    }

    public static implicit operator int(StableHashCode hash) => hash._value;
}
