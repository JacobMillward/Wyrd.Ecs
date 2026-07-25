namespace Wyrd.Ecs.Tests;

struct ScheduledPosition : IComponent { public float X; }
struct ScheduledHealth : IComponent { public int Value; }

sealed class MoveSystem : EcsSystem
{
    protected override void OnUpdate(World world, ulong tick) =>
        world.Query().With<Writes<ScheduledPosition>>().ForEach(0, (int _, ref ScheduledPosition p) => p.X += 1f);
}

sealed class DamageSystem : EcsSystem
{
    protected override void OnUpdate(World world, ulong tick) =>
        world.Query().With<Writes<ScheduledHealth>>().ForEach(0, (int _, ref ScheduledHealth h) => h.Value -= 1);
}

sealed class SpawnerSystem : EcsSystem
{
    protected override void OnUpdate(World world, ulong tick)
    {
        var entity = world.Commands.CreateEntity();
        world.Commands.AddComponent(entity, new ScheduledHealth { Value = 10 });
    }
}

public class ScheduledExecutorTests
{
    [Fact]
    public void DisjointSystems_BothRun_EachStageInSequence()
    {
        var access = new Dictionary<Type, SystemAccess>
        {
            [typeof(MoveSystem)] = new(Reads: [], Writes: [typeof(ScheduledPosition)]),
            [typeof(DamageSystem)] = new(Reads: [], Writes: [typeof(ScheduledHealth)]),
        };
        var (world, executor) = new WorldBuilder().WithSystems(access, new MoveSystem(), new DamageSystem()).BuildWithExecutor();
        var e = world.Commands.CreateEntity();
        world.Commands.AddComponent(e, new ScheduledPosition { X = 0f });
        world.Commands.AddComponent(e, new ScheduledHealth { Value = 5 });
        world.ApplyCommands();

        executor.RunTick(world, tick: 1);

        world.GetComponent<ScheduledPosition>(e).X.Should().Be(1f); // MoveSystem ran exactly once
        world.GetComponent<ScheduledHealth>(e).Value.Should().Be(4); // DamageSystem ran exactly once
    }

    [Fact]
    public void StructuralChangesFromASystem_AreVisibleAfterRunTick()
    {
        var access = new Dictionary<Type, SystemAccess>
        {
            [typeof(SpawnerSystem)] = new(Reads: [], Writes: [typeof(ScheduledHealth)]),
        };
        var (world, executor) = new WorldBuilder().WithSystems(access, new SpawnerSystem()).BuildWithExecutor();

        executor.RunTick(world, tick: 1);

        var count = 0;
        foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<ScheduledHealth>>().Resolve(world))
            count += chunk.Count;
        count.Should().Be(1); // SpawnerSystem's CreateEntity/AddComponent must be applied by RunTick, not left pending
    }

    [Fact]
    public void ConflictingSystems_BothStillRun_JustInSeparateStages()
    {
        var access = new Dictionary<Type, SystemAccess>
        {
            [typeof(MoveSystem)] = new(Reads: [], Writes: [typeof(ScheduledPosition)]),
        };
        var (world, executor) = new WorldBuilder().WithSystems(access, new MoveSystem(), new MoveSystem()).BuildWithExecutor();
        var e = world.Commands.CreateEntity();
        world.Commands.AddComponent(e, new ScheduledPosition { X = 0f });
        world.ApplyCommands();

        executor.RunTick(world, tick: 1);

        world.GetComponent<ScheduledPosition>(e).X.Should().Be(2f); // both MoveSystem instances ran, one per stage
    }
}
