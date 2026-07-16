namespace Wyrd.Ecs;

/// <summary>
/// Read-only chunk-level access to a <typeparamref name="T"/> component. Indexing
/// never marks anything dirty. The backing storage behind these members is an
/// archetype-storage-phase implementation detail — this phase fixes only the public
/// shape.
/// </summary>
public readonly ref struct Ref<T> : IComponentAccessor where T : struct, IComponent
{
    /// <summary>The number of components accessible through this instance.</summary>
    public int Length => throw new NotImplementedException();

    /// <summary>
    /// Returns a read-only reference to the <typeparamref name="T"/> component at
    /// <paramref name="index"/>.
    /// </summary>
    public ref readonly T this[int index] => throw new NotImplementedException();
}
