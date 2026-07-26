using BenchmarkDotNet.Attributes;
using Friflo.Engine.ECS;
using Comparison.Friflo;
// Friflo.Engine.ECS ships its own built-in Position type, colliding with our vocabulary's —
// disambiguate in favor of ours everywhere in this file.
using Position = Comparison.Friflo.Position;

namespace Comparison.EntityLifecycle;

public partial class EntityLifecycleBenchmarks
{
    private sealed class FrifloContext
    {
        public readonly EntityStore Store = new();
        public readonly Entity ToDispose;

        public FrifloContext()
        {
            ToDispose = Store.CreateEntity(new Position());
        }
    }

    [Context] private FrifloContext _friflo = null!;

    [Benchmark]
    public Entity Friflo_CreateBareEntity() => _friflo.Store.CreateEntity();

    [Benchmark]
    public Entity Friflo_CreateOneComponentEntity() => _friflo.Store.CreateEntity(new Position());

    [Benchmark]
    public Entity Friflo_CreateFourComponentEntity() =>
        _friflo.Store.CreateEntity(new Position(), new Velocity(), new Health(), new BulkPayload());

    [Benchmark]
    public Entity Friflo_CreateEightComponentEntity() =>
        _friflo.Store.CreateEntity(
            new Position(), new Velocity(), new Health(), new BulkPayload(),
            new Padding1(), new Padding2(), new Padding3(), new Padding4());

    [Benchmark]
    public void Friflo_DisposeEntity() => _friflo.ToDispose.DeleteEntity();
}
