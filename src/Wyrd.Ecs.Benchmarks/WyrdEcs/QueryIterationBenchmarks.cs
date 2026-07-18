using BenchmarkDotNet.Attributes;
using Wyrd.Ecs;

namespace Wyrd.Ecs.Benchmarks.WyrdEcs;

[MemoryDiagnoser]
public class QueryIterationBenchmarks
{
    private const int EntityCount = 10_000;

    [Params(false, true)]
    public bool Fragmented { get; set; }

    private World _world1NoConsumer = null!;
    private World _world1WithConsumer = null!;
    private World _world2NoConsumer = null!;
    private World _world2WithConsumer = null!;
    private ChangeConsumer<Position> _world1PositionConsumer = null!;
    private ChangeConsumer<Position> _world2PositionConsumer = null!;
    private ChangeConsumer<Velocity> _world2VelocityConsumer = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        (_world1NoConsumer, _world1WithConsumer) = BenchmarkWorlds.CreatePaired(PopulateOneComponent);
        (_world2NoConsumer, _world2WithConsumer) = BenchmarkWorlds.CreatePaired(PopulateTwoComponent);

        _world1PositionConsumer = _world1WithConsumer.RegisterChangeConsumer<Position>();
        _world2PositionConsumer = _world2WithConsumer.RegisterChangeConsumer<Position>();
        _world2VelocityConsumer = _world2WithConsumer.RegisterChangeConsumer<Velocity>();
    }

    private void PopulateOneComponent(World world)
    {
        for (var i = 0; i < EntityCount; i++)
        {
            var entity = world.CreateEntity();
            world.AddComponent<Position>(entity);
            if (Fragmented) Fragmentation.AddFragTag(world, entity, i);
        }
    }

    private void PopulateTwoComponent(World world)
    {
        for (var i = 0; i < EntityCount; i++)
        {
            var entity = world.CreateEntity();
            world.AddComponent<Position>(entity);
            world.AddComponent<Velocity>(entity);
            if (Fragmented) Fragmentation.AddFragTag(world, entity, i);
        }
    }

    [Benchmark(Baseline = true)]
    public void OneComponent_ChunkCallback_NoRegisteredConsumer() => OneComponentChunkCallback(_world1NoConsumer);

    /// <summary>
    /// Every `_WithRegisteredConsumer` benchmark in this suite advances its consumer(s)
    /// every call, the same rule <c>StructuralChangeBenchmarks</c> follows — otherwise the
    /// change log grows unboundedly across the whole job and the number measures log
    /// growth, not the tracked path's steady-state cost.
    /// </summary>
    [Benchmark]
    public void OneComponent_ChunkCallback_WithRegisteredConsumer()
    {
        OneComponentChunkCallback(_world1WithConsumer);
        _world1PositionConsumer.Advance(_world1WithConsumer.CurrentTick);
    }

    private static void OneComponentChunkCallback(World world)
    {
        world.AdvanceTick();
        world.Query<Mut<Position>>(chunk =>
        {
            for (var i = 0; i < chunk.Length; i++)
                chunk[i].X += chunk[i].Y * 0f;
        });
    }

    [Benchmark]
    public void OneComponent_HiddenChunkForEach_NoRegisteredConsumer() => OneComponentHiddenChunkForEach(_world1NoConsumer);

    [Benchmark]
    public void OneComponent_HiddenChunkForEach_WithRegisteredConsumer()
    {
        OneComponentHiddenChunkForEach(_world1WithConsumer);
        _world1PositionConsumer.Advance(_world1WithConsumer.CurrentTick);
    }

    private static void OneComponentHiddenChunkForEach(World world)
    {
        world.AdvanceTick();
        foreach (var row in world.Query<Position>())
            row.Get<Position>().X += row.Get<Position>().Y * 0f;
    }

    [Benchmark]
    public void TwoComponent_ChunkCallback_NoRegisteredConsumer() => TwoComponentChunkCallback(_world2NoConsumer);

    [Benchmark]
    public void TwoComponent_ChunkCallback_WithRegisteredConsumer()
    {
        TwoComponentChunkCallback(_world2WithConsumer);
        _world2PositionConsumer.Advance(_world2WithConsumer.CurrentTick);
        _world2VelocityConsumer.Advance(_world2WithConsumer.CurrentTick);
    }

    private static void TwoComponentChunkCallback(World world)
    {
        world.AdvanceTick();
        world.Query<Mut<Position>, Ref<Velocity>>((position, velocity) =>
        {
            for (var i = 0; i < position.Length; i++)
                position[i].X += velocity[i].X * 0f;
        });
    }
}
