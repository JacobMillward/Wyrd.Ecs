namespace Wyrd.Ecs;

/// <summary>
/// Base class for a system with one query and one callback. Override
/// <see cref="DefineQuery"/> to declare the query, and add an `Update` method whose
/// parameters match the query's component shape in order; the query-chain generator
/// recognizes `Update` by name and supplies <see cref="EcsSystem.Execute"/>.
/// </summary>
public abstract class QuerySystem : EcsSystem
{
    /// <summary>
    /// This system's query chain, as a compile-time-only declaration. The query-chain
    /// generator reads this override's return *expression*'s real type via the semantic
    /// model, never its declared `IQuery` return type, so editing the chain (adding a
    /// component, reordering) only ever touches this method's body. Starts from the empty
    /// chain rather than `World`, since every override's first move was `world.Query()`
    /// anyway; reach `query.World` if the world itself is needed.
    /// </summary>
    protected abstract IQuery DefineQuery(Query query);
}
