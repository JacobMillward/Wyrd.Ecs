namespace Wyrd.Ecs;

/// <summary>
/// Tracked, mutable chunk-level access to a <typeparamref name="T"/> component.
/// Indexing marks the specific entity dirty before returning a reference to its
/// component slot — this is the tracked mutation path; there is no separate untracked
/// accessor. The backing storage and dirty-marking plumbing behind these members are
/// an archetype-storage-phase implementation detail — this phase fixes only the
/// public shape.
/// </summary>
public readonly ref struct Mut<T> : IComponentAccessor where T : struct, IComponent
{
    /// <summary>The number of components accessible through this instance.</summary>
    public int Length => throw new NotImplementedException();

    /// <summary>
    /// Marks the entity at <paramref name="index"/> dirty, then returns a mutable
    /// reference to its <typeparamref name="T"/> component.
    /// </summary>
    public ref T this[int index] => throw new NotImplementedException();
}
