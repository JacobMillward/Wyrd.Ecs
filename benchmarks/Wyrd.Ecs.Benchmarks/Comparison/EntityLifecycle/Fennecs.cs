using BenchmarkDotNet.Attributes;
using fennecs;
using Comparison.Fennecs;

namespace Comparison.EntityLifecycle;

public partial class EntityLifecycleBenchmarks
{
    private sealed class FennecsContext
    {
        public readonly World World = new();
        public readonly Entity ToDispose;

        public FennecsContext()
        {
            ToDispose = World.Spawn().Add(new Position());
        }
    }

    [Context] private FennecsContext _fennecs = null!;

    [Benchmark]
    public Entity Fennecs_CreateBareEntity() => _fennecs.World.Spawn();

    [Benchmark]
    public Entity Fennecs_CreateOneComponentEntity() => _fennecs.World.Spawn().Add(new Position());

    [Benchmark]
    public Entity Fennecs_CreateFourComponentEntity() =>
        _fennecs.World.Spawn().Add(new Position()).Add(new Velocity()).Add(new Health()).Add(new BulkPayload());

    [Benchmark]
    public Entity Fennecs_CreateEightComponentEntity() =>
        _fennecs.World.Spawn()
            .Add(new Position()).Add(new Velocity()).Add(new Health()).Add(new BulkPayload())
            .Add(new Padding1()).Add(new Padding2()).Add(new Padding3()).Add(new Padding4());

    [Benchmark]
    public void Fennecs_DisposeEntity() => _fennecs.ToDispose.Despawn();
}
