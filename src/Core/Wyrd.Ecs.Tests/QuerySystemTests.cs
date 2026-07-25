namespace Wyrd.Ecs.Tests;

struct Energy : IComponent { public float Current; public float DrainPerSecond; }

sealed partial class DrainSystem : QuerySystem
{
    private static Query<(Writes<Energy>, Nil)> Build(World world) => world.Query().With<Writes<Energy>>();

    private partial void Execute(ulong tick, ref Energy energy) => energy.Current -= energy.DrainPerSecond;
}

public class QuerySystemTests
{
    [Fact]
    public void DeclaredSystem_RunOnce_MutatesThroughToRealStorage()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity(new Energy { Current = 100f, DrainPerSecond = 10f });
        world.ApplyCommands();

        new DrainSystem().RunOnce(world, tick: 0);

        world.GetComponent<Energy>(entity).Current.Should().Be(90f);
    }

    [Fact]
    public void DeclaredSystem_RunOnce_VisitsEveryMatchingEntity()
    {
        var world = new World();
        var entities = new Entity[5];
        for (var i = 0; i < entities.Length; i++)
            entities[i] = world.Commands.CreateEntity(new Energy { Current = 100f, DrainPerSecond = 1f });
        world.ApplyCommands();

        new DrainSystem().RunOnce(world, tick: 0);

        foreach (var entity in entities)
            world.GetComponent<Energy>(entity).Current.Should().Be(99f);
    }

    [Fact]
    public void DeclaredSystem_RegistersGeneratedSystemAccess()
    {
        Wyrd.Ecs.Generated.GeneratedSystemAccess.Entries.Should().ContainKey(typeof(DrainSystem));
        var access = Wyrd.Ecs.Generated.GeneratedSystemAccess.Entries[typeof(DrainSystem)];
        access.Writes.Should().Equal(typeof(Energy));
        access.Reads.Should().BeEmpty();
    }
}
