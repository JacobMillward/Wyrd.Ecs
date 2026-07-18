using BenchmarkDotNet.Attributes;
using Friflo.Engine.ECS;

namespace FrifloBenchmarks;

[MemoryDiagnoser]
public class EntityLifecycleBenchmarks
{
    private EntityStore _store = null!;
    private Entity _toDispose;

    [IterationSetup]
    public void IterationSetup()
    {
        _store = new EntityStore();
        _toDispose = _store.CreateEntity(new Position());
    }

    [Benchmark(Baseline = true)]
    public Entity CreateBareEntity() => _store.CreateEntity();

    [Benchmark]
    public Entity CreateOneComponentEntity() => _store.CreateEntity(new Position());

    [Benchmark]
    public Entity CreateFourComponentEntity() =>
        _store.CreateEntity(new Position(), new Velocity(), new Health(), new BulkPayload());

    [Benchmark]
    public Entity CreateEightComponentEntity() =>
        _store.CreateEntity(
            new Position(), new Velocity(), new Health(), new BulkPayload(),
            new Padding1(), new Padding2(), new Padding3(), new Padding4());

    [Benchmark]
    public void DisposeEntity() => _toDispose.DeleteEntity();
}
