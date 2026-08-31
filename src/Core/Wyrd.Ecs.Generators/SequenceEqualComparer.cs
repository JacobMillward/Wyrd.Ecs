using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Wyrd.Ecs.Generators;

/// <summary>
/// Structural equality for an <c>ImmutableArray&lt;T&gt;</c>-typed incremental value.
/// <c>ImmutableArray&lt;T&gt;.Equals</c> compares the backing array by reference, so two
/// separately-built arrays with identical elements compare unequal by default. Pass an
/// instance to <c>IncrementalValuesProvider&lt;T&gt;.Collect().WithComparer(...)</c> so a
/// pipeline stage recognizes an unchanged element set even when it produces a new array
/// instance.
/// </summary>
internal sealed class SequenceEqualComparer<T> : IEqualityComparer<ImmutableArray<T>>
{
    public static readonly SequenceEqualComparer<T> Instance = new();

    private SequenceEqualComparer() { }

    public bool Equals(ImmutableArray<T> x, ImmutableArray<T> y) => x.SequenceEqual(y);

    public int GetHashCode(ImmutableArray<T> array) => StableHashCode.Start(array.Length).AddEach(array);
}
