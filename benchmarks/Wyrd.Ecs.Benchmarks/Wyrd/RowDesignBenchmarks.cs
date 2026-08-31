using BenchmarkDotNet.Attributes;
using Wyrd.Ecs;
using Comparison.Wyrd;

namespace Wyrd.Ecs.Benchmarks.Wyrd;

/// <summary>
/// Settles the open cost question from the composable-systems design discussion: does a
/// per-row EntityView-style reference (for <c>row.Entity.DestroyEntity()</c>-shaped
/// ergonomics) cost anything measurable over a row carrying just a plain <see cref="Entity"/>,
/// given identical per-row component work. Both hand-rolled rows walk the real
/// <see cref="ArchetypeQuery.Resolve(World)"/>/<see cref="Mut{T}"/>/<see cref="Ref{T}"/>
/// chunk machinery <see cref="TrackedQueryIterationBenchmarks"/> already exercises -- nothing
/// simulated. Neither row variant calls <c>DestroyEntity</c>/<c>AddComponent</c> in the timed
/// loop: that queueing cost is a separate, already-covered question
/// (<see cref="CommandBatchBenchmarks"/>); this isolates only the cost of carrying and
/// constructing the extra field.
/// </summary>
[MemoryDiagnoser]
public class RowDesignBenchmarks
{
    private const int EntityCount = 10_000;

    private static readonly ArchetypeQuery TwoComponentQuery =
        ArchetypeQuery.Empty.Access<Mut<Position>>().Access<Ref<Velocity>>();

    private World _world = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _world = new World();
        for (var i = 0; i < EntityCount; i++)
            _world.Commands.CreateEntity(new Position(), new Velocity { X = 1f });
        _world.ApplyCommands();
    }

    [Benchmark(Baseline = true)]
    public void FluentChain_Delegate()
    {
        _world.AdvanceTick();
        _world.Query().With<Position>().With<Velocity>()
            .ForEach(0, (in int _, ref Position p, in Velocity v) => p.X += v.X * 0f);
    }

    [Benchmark]
    public void StructEnumerator_PlainEntityRow()
    {
        _world.AdvanceTick();
        foreach (var chunk in TwoComponentQuery.Resolve(_world))
        {
            var positions = chunk.Access<Mut<Position>>();
            var velocities = chunk.Access<Ref<Velocity>>();
            var entities = chunk.Entities;
            for (var i = 0; i < chunk.Count; i++)
            {
                var row = new PlainRow(entities[i], ref positions[i], in velocities[i]);
                row.Position.X += row.Velocity.X * 0f;
            }
        }
    }

    [Benchmark]
    public void StructEnumerator_CommandBufferRow()
    {
        _world.AdvanceTick();
        var commands = _world.Commands;
        foreach (var chunk in TwoComponentQuery.Resolve(_world))
        {
            var positions = chunk.Access<Mut<Position>>();
            var velocities = chunk.Access<Ref<Velocity>>();
            var entities = chunk.Entities;
            for (var i = 0; i < chunk.Count; i++)
            {
                var row = new CommandRow(entities[i], ref positions[i], in velocities[i], commands);
                row.Position.X += row.Velocity.X * 0f;
            }
        }
    }
}

/// <summary>Stand-in for what the [Query]-property design's generator would emit for a row with no EntityView-style ergonomics: entity plus component refs only.</summary>
public ref struct PlainRow
{
    public Entity Entity;
    public ref Position Position;
    public ref readonly Velocity Velocity;

    public PlainRow(Entity entity, ref Position position, in Velocity velocity)
    {
        Entity = entity;
        Position = ref position;
        Velocity = ref velocity;
    }
}

/// <summary>Same as <see cref="PlainRow"/>, plus a real <see cref="CommandBuffer"/> reference so <c>row.DestroyEntity()</c>/<c>row.AddComponent()</c> would work -- the field under test.</summary>
public ref struct CommandRow
{
    public Entity Entity;
    public ref Position Position;
    public ref readonly Velocity Velocity;
    private readonly CommandBuffer _commands;

    public CommandRow(Entity entity, ref Position position, in Velocity velocity, CommandBuffer commands)
    {
        Entity = entity;
        Position = ref position;
        Velocity = ref velocity;
        _commands = commands;
    }

    public readonly void DestroyEntity() => _commands.DestroyEntity(Entity);
}
