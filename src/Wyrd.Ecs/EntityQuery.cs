namespace Wyrd.Ecs;

/// <summary>
/// The hidden-chunk convenience tier: a <c>foreach</c>-able sequence of one component
/// accessor per matching entity, walking archetypes and chunks internally so no chunk
/// or archetype vocabulary is required to write a query. Returned by
/// <see cref="IWorld.Query{TAccess0}()"/>; supersedes the scaffold phase's
/// <c>EntityAction</c>/<c>ForEach</c> entirely, not alongside it. The enumerator's
/// backing plumbing connecting it to the chunk-level <typeparamref name="TAccess0"/>
/// indexer is an archetype-storage-phase implementation detail — this phase fixes
/// only the public shape.
/// </summary>
public readonly ref struct EntityQuery<TAccess0> where TAccess0 : struct, IComponentAccessor<TAccess0>, allows ref struct
{
    /// <summary>Returns the enumerator for this query.</summary>
    public Enumerator GetEnumerator() => throw new NotImplementedException();

    /// <summary>Enumerates one <typeparamref name="TAccess0"/> accessor per matching entity.</summary>
    public ref struct Enumerator
    {
        /// <summary>The current entity's component accessor.</summary>
        public TAccess0 Current => throw new NotImplementedException();

        /// <summary>Advances to the next matching entity.</summary>
        public bool MoveNext() => throw new NotImplementedException();
    }
}
