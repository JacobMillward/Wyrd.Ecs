using BenchmarkDotNet.Attributes;
using Wyrd.Ecs;
using Comparison.Wyrd;

namespace Wyrd.Ecs.Benchmarks.Wyrd;

/// <summary>
/// The <see cref="Tracked"/> dimension, Wyrd.Ecs-only with no Friflo or fennecs equivalent.
/// See <see cref="TrackedEntityLifecycleBenchmarks"/> for the same reasoning.
/// </summary>
[MemoryDiagnoser]
public class TrackedStructuralChangeBenchmarks
{
    [Params(false, true)]
    public bool Tracked { get; set; }

    private World _world = null!;
    private Entity _entity;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _world = new World();
        _entity = _world.Commands.CreateEntity(new Position(), new Velocity());
        _world.ApplyCommands();

        if (Tracked)
        {
            _world.TrackChanges<Position>();
            _world.TrackChanges<BulkPayload>();
        }
    }

    [Benchmark(Baseline = true)]
    public void MutateExistingComponent()
    {
        _world.AdvanceTick();
        ref var position = ref _world.GetComponent<Position>(_entity);
        position.X += 0f;
    }

    [Benchmark]
    public void AddRemoveComponent_ArchetypeMove()
    {
        _world.AdvanceTick();
        _world.Commands.AddComponent(_entity, new BulkPayload());
        _world.Commands.RemoveComponent<BulkPayload>(_entity);
        _world.ApplyCommands();
    }

    [Benchmark]
    public void AddRemoveTag_ArchetypeMove()
    {
        _world.AdvanceTick();
        _world.Commands.AddTag<Marker>(_entity);
        _world.Commands.RemoveTag<Marker>(_entity);
        _world.ApplyCommands();
    }
}
