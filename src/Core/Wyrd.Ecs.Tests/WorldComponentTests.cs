namespace Wyrd.Ecs.Tests;

public class WorldComponentTests
{
    private struct Position : IComponent
    {
        public float X;
    }

    private struct Velocity : IComponent
    {
        public float X;
    }

    [Fact]
    public void AddComponent_EntityThenHasIt()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.Commands.AddComponent(entity, new Position());
        world.ApplyCommands();

        world.HasComponent<Position>(entity).Should().BeTrue();
    }

    [Fact]
    public void AddComponent_StoresTheGivenValue()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.Commands.AddComponent(entity, new Position { X = 3f });
        world.ApplyCommands();

        world.GetComponent<Position>(entity).X.Should().Be(3f);
    }

    /// <summary>
    /// Adding Velocity clones the {Position} archetype's storage into a new
    /// {Position, Velocity} archetype via a different construction path than
    /// GetOrCreateStorage; the clone must size the same way or entities past a small
    /// default index out of range as it grows.
    /// </summary>
    [Fact]
    public void AddComponent_ClonedArchetypeStorage_GrowsWithTheArchetype()
    {
        var world = new WorldBuilder().WithArchetypeCapacity(16).Build();
        world.Commands.CreateEntity(new Position());

        var entities = new Entity[10];
        for (var i = 0; i < entities.Length; i++)
        {
            entities[i] = world.Commands.CreateEntity(new Position { X = i });
            world.Commands.AddComponent(entities[i], new Velocity());
        }
        world.ApplyCommands();

        for (var i = 0; i < entities.Length; i++)
            world.GetComponent<Position>(entities[i]).X.Should().Be(i);
    }

    [Fact]
    public void AddComponent_Twice_Overwrites()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity(new Position { X = 1f });
        world.Commands.AddComponent(entity, new Position { X = 2f });

        var act = () => world.ApplyCommands();

        act.Should().NotThrow();
        world.GetComponent<Position>(entity).X.Should().Be(2f);
    }

    [Fact]
    public void GetComponent_Missing_Throws()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        var act = () => world.GetComponent<Position>(entity);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TryGetComponent_Missing_ReturnsNotFound()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.TryGetComponent<Position>(entity, out var found);

        found.Should().BeFalse();
    }

    [Fact]
    public void TryGetComponent_Present_ReturnsFoundAndTheTrackedValue()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity(new Position { X = 5f });
        world.ApplyCommands();

        ref var value = ref world.TryGetComponent<Position>(entity, out var found);

        found.Should().BeTrue();
        value.X.Should().Be(5f);
    }

    [Fact]
    public void TryGetComponent_Present_ReturnedRefWritesThrough()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity(new Position { X = 5f });
        world.ApplyCommands();

        ref var value = ref world.TryGetComponent<Position>(entity, out _);
        value.X = 9f;

        world.GetComponent<Position>(entity).X.Should().Be(9f);
    }

    [Fact]
    public void HasComponent_Missing_ReturnsFalse()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.HasComponent<Position>(entity).Should().BeFalse();
    }

    [Fact]
    public void RemoveComponent_Present_RemovesIt()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity(new Position());
        world.ApplyCommands();

        world.Commands.RemoveComponent<Position>(entity);
        world.ApplyCommands();

        world.HasComponent<Position>(entity).Should().BeFalse();
    }

    [Fact]
    public void RemoveComponent_Missing_IsANoOp()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.Commands.RemoveComponent<Position>(entity);
        var act = () => world.ApplyCommands();

        act.Should().NotThrow();
    }

    [Fact]
    public void ArchetypeMove_AddingASecondComponent_PreservesTheFirstOnesValue()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity(new Position { X = 11f });
        world.ApplyCommands();

        world.Commands.AddComponent(entity, new Velocity { X = 22f });
        world.ApplyCommands();

        world.GetComponent<Position>(entity).X.Should().Be(11f);
        world.GetComponent<Velocity>(entity).X.Should().Be(22f);
    }

    [Fact]
    public void ArchetypeMove_RemovingOneComponent_PreservesTheOthersValue()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity(new Position { X = 11f }, new Velocity { X = 22f });
        world.ApplyCommands();

        world.Commands.RemoveComponent<Position>(entity);
        world.ApplyCommands();

        world.HasComponent<Position>(entity).Should().BeFalse();
        world.GetComponent<Velocity>(entity).X.Should().Be(22f);
    }

    [Fact]
    public void ArchetypeMove_DoesNotDisturbOtherEntitiesInTheSourceArchetype()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity(new Position { X = 1f });
        Entity b = world.Commands.CreateEntity(new Position { X = 2f });
        world.ApplyCommands();

        world.Commands.AddComponent(a, new Velocity());
        world.ApplyCommands();

        world.GetComponent<Position>(b).X.Should().Be(2f);
    }

    [Fact]
    public void ArchetypeMove_SharedTargetArchetype_BothEntitiesKeepTheirOwnValues()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity(new Position { X = 1f });
        Entity b = world.Commands.CreateEntity(new Position { X = 2f });
        world.ApplyCommands();

        world.Commands.AddComponent(a, new Velocity { X = 10f });
        world.Commands.AddComponent(b, new Velocity { X = 20f });
        world.ApplyCommands();

        world.GetComponent<Position>(a).X.Should().Be(1f);
        world.GetComponent<Velocity>(a).X.Should().Be(10f);
        world.GetComponent<Position>(b).X.Should().Be(2f);
        world.GetComponent<Velocity>(b).X.Should().Be(20f);
    }

    [Fact]
    public void DestroyEntity_WithComponents_DoesNotCorruptSurvivingEntity()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity(new Position { X = 1f });
        Entity b = world.Commands.CreateEntity(new Position { X = 2f });
        world.ApplyCommands();

        world.Commands.DestroyEntity(a);
        world.ApplyCommands();

        world.GetComponent<Position>(b).X.Should().Be(2f);
    }
}
