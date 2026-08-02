using BenchmarkDotNet.Attributes;
using Friflo.Engine.ECS;
using Comparison.Friflo;
// Friflo.Engine.ECS ships its own built-in Position type, colliding with our vocabulary's.
// Disambiguate in favor of ours everywhere in this file.
using Position = Comparison.Friflo.Position;

namespace Comparison.StructuralChange;

public partial class StructuralChangeBenchmarks
{
    private sealed class FrifloContext
    {
        public readonly EntityStore Store = new();
        public readonly Entity Entity;

        public FrifloContext()
        {
            Entity = Store.CreateEntity(new Position(), new Velocity());
        }
    }

    [Context] private FrifloContext _friflo = null!;

    [Benchmark]
    public void Friflo_MutateExistingComponent()
    {
        ref var position = ref _friflo.Entity.GetComponent<Position>();
        position.X += 0f;
    }

    [Benchmark]
    public void Friflo_AddRemoveComponent_ArchetypeMove()
    {
        _friflo.Entity.AddComponent(new BulkPayload());
        _friflo.Entity.RemoveComponent<BulkPayload>();
    }

    [Benchmark]
    public void Friflo_AddRemoveTag_ArchetypeMove()
    {
        _friflo.Entity.AddTag<Marker>();
        _friflo.Entity.RemoveTag<Marker>();
    }
}
