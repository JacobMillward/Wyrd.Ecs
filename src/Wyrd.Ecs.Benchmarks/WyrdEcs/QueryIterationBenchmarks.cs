using BenchmarkDotNet.Attributes;
using Wyrd.Ecs;

namespace Wyrd.Ecs.Benchmarks.WyrdEcs;

[MemoryDiagnoser]
public class QueryIterationBenchmarks
{
    private const int EntityCount = 10_000;

    [Params(false, true)]
    public bool Fragmented { get; set; }

    private World _world1 = null!;
    private World _world2 = null!;
    private ChangeConsumer<Position> _world1PositionConsumer = null!;
    private ChangeConsumer<Position> _world2PositionConsumer = null!;
    private ChangeConsumer<Velocity> _world2VelocityConsumer = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _world1 = new World();
        _world2 = new World();
        _world1PositionConsumer = _world1.RegisterChangeConsumer<Position>();
        _world2PositionConsumer = _world2.RegisterChangeConsumer<Position>();
        _world2VelocityConsumer = _world2.RegisterChangeConsumer<Velocity>();

        for (var i = 0; i < EntityCount; i++)
        {
            var e1 = _world1.CreateEntity();
            _world1.AddComponent<Position>(e1);
            var e2 = _world2.CreateEntity();
            _world2.AddComponent<Position>(e2);
            _world2.AddComponent<Velocity>(e2);

            if (Fragmented)
            {
                Fragmentation.AddFragTag(_world1, e1, i);
                Fragmentation.AddFragTag(_world2, e2, i);
            }
        }
    }

    [Benchmark(Baseline = true)]
    public void OneComponent_ChunkCallback()
    {
        _world1.AdvanceTick();
        _world1.Query<Mut<Position>>(chunk =>
        {
            for (var i = 0; i < chunk.Length; i++)
                chunk[i].X += chunk[i].Y * 0f;
        });
    }

    [Benchmark]
    public void OneComponent_HiddenChunkForEach()
    {
        _world1.AdvanceTick();
        foreach (var row in _world1.Query<Position>())
            row.Get<Position>().X += row.Get<Position>().Y * 0f;
    }

    [Benchmark]
    public void TwoComponent_ChunkCallback()
    {
        _world2.AdvanceTick();
        _world2.Query<Mut<Position>, Ref<Velocity>>((position, velocity) =>
        {
            for (var i = 0; i < position.Length; i++)
                position[i].X += velocity[i].X * 0f;
        });
    }
}
