using Wyrd.Ecs;

namespace Wyrd.Ecs.Benchmarks.WyrdEcs;

/// <summary>
/// Builds a paired (no-consumer, with-consumer) <see cref="World"/> for benchmarks that
/// compare tracked vs. untracked cost against otherwise-identical state. <c>populate</c>
/// runs once per world so both start from the same steps; callers register whatever
/// <see cref="ChangeConsumer{T}"/>s they need on the with-consumer world afterward, since
/// which types to track varies per benchmark class.
/// </summary>
internal static class BenchmarkWorlds
{
    public static (World NoConsumer, World WithConsumer) CreatePaired(Action<World> populate)
    {
        var noConsumer = new World();
        var withConsumer = new World();
        populate(noConsumer);
        populate(withConsumer);
        return (noConsumer, withConsumer);
    }

    /// <summary>
    /// Same as <see cref="CreatePaired(Action{World})"/>, for benchmarks that also need a
    /// handle to a specific entity <c>populate</c> created. Both worlds run the identical
    /// population steps starting from a fresh <see cref="World"/>, so the same
    /// <see cref="Entity"/> value (id and generation) is valid against either one.
    /// </summary>
    public static (World NoConsumer, World WithConsumer, Entity Entity) CreatePaired(Func<World, Entity> populate)
    {
        var noConsumer = new World();
        var withConsumer = new World();
        populate(noConsumer);
        var entity = populate(withConsumer);
        return (noConsumer, withConsumer, entity);
    }
}
