using BenchmarkDotNet.Attributes;
using Wyrd.Ecs;
using Comparison.Wyrd;

namespace Comparison.StructuralChange;

public partial class StructuralChangeBenchmarks
{
    private sealed class WyrdContext
    {
        public readonly World World = new();
        public readonly Entity Entity;

        public WyrdContext()
        {
            Entity = World.Commands.CreateEntity(new Position(), new Velocity());
            World.ApplyCommands();
        }
    }

    [Context] private WyrdContext _wyrd = null!;

    [Benchmark(Baseline = true)]
    public void Wyrd_MutateExistingComponent()
    {
        ref var position = ref _wyrd.World.GetComponent<Position>(_wyrd.Entity);
        position.X += 0f;
    }

    [Benchmark]
    public void Wyrd_AddRemoveComponent_ArchetypeMove()
    {
        _wyrd.World.Commands.AddComponent(_wyrd.Entity, new BulkPayload());
        _wyrd.World.Commands.RemoveComponent<BulkPayload>(_wyrd.Entity);
        _wyrd.World.ApplyCommands();
    }

    [Benchmark]
    public void Wyrd_AddRemoveTag_ArchetypeMove()
    {
        _wyrd.World.Commands.AddTag<Marker>(_wyrd.Entity);
        _wyrd.World.Commands.RemoveTag<Marker>(_wyrd.Entity);
        _wyrd.World.ApplyCommands();
    }
}
