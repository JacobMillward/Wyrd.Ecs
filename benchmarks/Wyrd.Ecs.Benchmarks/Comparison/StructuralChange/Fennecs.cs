using BenchmarkDotNet.Attributes;
using fennecs;
using Comparison.Fennecs;

namespace Comparison.StructuralChange;

public partial class StructuralChangeBenchmarks
{
    private sealed class FennecsContext
    {
        public readonly World World = new();
        public readonly Entity Entity;

        public FennecsContext()
        {
            Entity = World.Spawn().Add(new Position()).Add(new Velocity());
        }
    }

    [Context] private FennecsContext _fennecs = null!;

    [Benchmark]
    public void Fennecs_MutateExistingComponent()
    {
        ref var position = ref _fennecs.Entity.Ref<Position>();
        position.X += 0f;
    }

    [Benchmark]
    public void Fennecs_AddRemoveComponent_ArchetypeMove()
    {
        _fennecs.Entity.Add(new BulkPayload());
        _fennecs.Entity.Remove<BulkPayload>();
    }

    [Benchmark]
    public void Fennecs_AddRemoveTag_ArchetypeMove()
    {
        _fennecs.Entity.Add(new Marker());
        _fennecs.Entity.Remove<Marker>();
    }
}
