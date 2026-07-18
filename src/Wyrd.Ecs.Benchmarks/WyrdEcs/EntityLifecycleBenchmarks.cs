using BenchmarkDotNet.Attributes;
using Wyrd.Ecs;

namespace Wyrd.Ecs.Benchmarks.WyrdEcs;

/// <summary>
/// Unlike <see cref="StructuralChangeBenchmarks"/> and <see cref="QueryIterationBenchmarks"/>,
/// this class can't build its no-consumer/with-consumer worlds once in
/// <c>[GlobalSetup]</c> via <see cref="BenchmarkWorlds"/>: every benchmark here mutates the
/// world's entity/archetype table itself (that's the thing being measured), so each needs a
/// fresh <see cref="World"/> per invocation, rebuilt in <c>[IterationSetup]</c>.
/// </summary>
[MemoryDiagnoser]
public class EntityLifecycleBenchmarks
{
    private World _noConsumer = null!;
    private World _withConsumer = null!;
    private Entity _toDisposeNoConsumer;
    private Entity _toDisposeWithConsumer;

    [IterationSetup]
    public void IterationSetup()
    {
        _noConsumer = new World();
        _toDisposeNoConsumer = _noConsumer.CreateEntity();
        _noConsumer.AddComponent<Position>(_toDisposeNoConsumer);

        _withConsumer = new World();
        _withConsumer.RegisterChangeConsumer<Position>();
        _withConsumer.RegisterChangeConsumer<Velocity>();
        _withConsumer.RegisterChangeConsumer<Health>();
        _withConsumer.RegisterChangeConsumer<BulkPayload>();
        _withConsumer.RegisterChangeConsumer<Padding1>();
        _withConsumer.RegisterChangeConsumer<Padding2>();
        _withConsumer.RegisterChangeConsumer<Padding3>();
        _withConsumer.RegisterChangeConsumer<Padding4>();
        _toDisposeWithConsumer = _withConsumer.CreateEntity();
        _withConsumer.AddComponent<Position>(_toDisposeWithConsumer);
    }

    [Benchmark(Baseline = true)]
    public Entity CreateBareEntity_NoRegisteredConsumer() => CreateBareEntity(_noConsumer);

    [Benchmark]
    public Entity CreateBareEntity_WithRegisteredConsumer() => CreateBareEntity(_withConsumer);

    private static Entity CreateBareEntity(World world) => world.CreateEntity();

    [Benchmark]
    public Entity CreateOneComponentEntity_NoRegisteredConsumer() => CreateOneComponentEntity(_noConsumer);

    [Benchmark]
    public Entity CreateOneComponentEntity_WithRegisteredConsumer() => CreateOneComponentEntity(_withConsumer);

    private static Entity CreateOneComponentEntity(World world) => world.CreateEntity(new Position());

    [Benchmark]
    public Entity CreateFourComponentEntity_NoRegisteredConsumer() => CreateFourComponentEntity(_noConsumer);

    [Benchmark]
    public Entity CreateFourComponentEntity_WithRegisteredConsumer() => CreateFourComponentEntity(_withConsumer);

    private static Entity CreateFourComponentEntity(World world) =>
        world.CreateEntity(new Position(), new Velocity(), new Health(), new BulkPayload());

    [Benchmark]
    public Entity CreateEightComponentEntity_NoRegisteredConsumer() => CreateEightComponentEntity(_noConsumer);

    [Benchmark]
    public Entity CreateEightComponentEntity_WithRegisteredConsumer() => CreateEightComponentEntity(_withConsumer);

    private static Entity CreateEightComponentEntity(World world) =>
        world.CreateEntity(
            new Position(), new Velocity(), new Health(), new BulkPayload(),
            new Padding1(), new Padding2(), new Padding3(), new Padding4());

    /// <summary>
    /// Creates an entity empty, then adds each component one at a time, moving through an
    /// intermediate archetype per add. Kept alongside the batched
    /// <c>CreateFourComponentEntity</c> pair to show the cost of that pattern against the
    /// batched <c>CreateEntity{T...}</c> overloads directly, in both tracked and untracked
    /// form.
    /// </summary>
    [Benchmark]
    public Entity CreateFourComponentEntity_OneAtATime_NoRegisteredConsumer() =>
        CreateFourComponentEntityOneAtATime(_noConsumer);

    /// <inheritdoc cref="CreateFourComponentEntity_OneAtATime_NoRegisteredConsumer"/>
    [Benchmark]
    public Entity CreateFourComponentEntity_OneAtATime_WithRegisteredConsumer() =>
        CreateFourComponentEntityOneAtATime(_withConsumer);

    private static Entity CreateFourComponentEntityOneAtATime(World world)
    {
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity);
        world.AddComponent<Velocity>(entity);
        world.AddComponent<Health>(entity);
        world.AddComponent<BulkPayload>(entity);
        return entity;
    }

    /// <inheritdoc cref="CreateFourComponentEntity_OneAtATime_NoRegisteredConsumer"/>
    [Benchmark]
    public Entity CreateEightComponentEntity_OneAtATime_NoRegisteredConsumer() =>
        CreateEightComponentEntityOneAtATime(_noConsumer);

    /// <inheritdoc cref="CreateFourComponentEntity_OneAtATime_NoRegisteredConsumer"/>
    [Benchmark]
    public Entity CreateEightComponentEntity_OneAtATime_WithRegisteredConsumer() =>
        CreateEightComponentEntityOneAtATime(_withConsumer);

    private static Entity CreateEightComponentEntityOneAtATime(World world)
    {
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity);
        world.AddComponent<Velocity>(entity);
        world.AddComponent<Health>(entity);
        world.AddComponent<BulkPayload>(entity);
        world.AddComponent<Padding1>(entity);
        world.AddComponent<Padding2>(entity);
        world.AddComponent<Padding3>(entity);
        world.AddComponent<Padding4>(entity);
        return entity;
    }

    [Benchmark]
    public void DisposeEntity_NoRegisteredConsumer() => DisposeEntity(_noConsumer, _toDisposeNoConsumer);

    [Benchmark]
    public void DisposeEntity_WithRegisteredConsumer() => DisposeEntity(_withConsumer, _toDisposeWithConsumer);

    private static void DisposeEntity(World world, Entity entity) => world.DestroyEntity(entity);
}
