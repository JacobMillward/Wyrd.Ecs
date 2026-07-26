using BenchmarkDotNet.Attributes;
using Wyrd.Ecs;
using Comparison.Wyrd;

namespace Comparison.EntityLifecycle;

public partial class EntityLifecycleBenchmarks
{
    private sealed class WyrdContext
    {
        public readonly World World = new();
        public readonly Entity ToDispose;

        public WyrdContext()
        {
            ToDispose = World.Commands.CreateEntity(new Position());
            World.ApplyCommands();
        }
    }

    [Context] private WyrdContext _wyrd = null!;

    [Benchmark(Baseline = true)]
    public Entity Wyrd_CreateBareEntity()
    {
        var entity = _wyrd.World.Commands.CreateEntity();
        _wyrd.World.ApplyCommands();
        return entity;
    }

    [Benchmark]
    public Entity Wyrd_CreateOneComponentEntity()
    {
        var entity = _wyrd.World.Commands.CreateEntity(new Position());
        _wyrd.World.ApplyCommands();
        return entity;
    }

    [Benchmark]
    public Entity Wyrd_CreateFourComponentEntity()
    {
        var entity = _wyrd.World.Commands.CreateEntity(new Position(), new Velocity(), new Health(), new BulkPayload());
        _wyrd.World.ApplyCommands();
        return entity;
    }

    [Benchmark]
    public Entity Wyrd_CreateEightComponentEntity()
    {
        var entity = _wyrd.World.Commands.CreateEntity(
            new Position(), new Velocity(), new Health(), new BulkPayload(),
            new Padding1(), new Padding2(), new Padding3(), new Padding4());
        _wyrd.World.ApplyCommands();
        return entity;
    }

    [Benchmark]
    public void Wyrd_DisposeEntity()
    {
        _wyrd.World.Commands.DestroyEntity(_wyrd.ToDispose);
        _wyrd.World.ApplyCommands();
    }
}
