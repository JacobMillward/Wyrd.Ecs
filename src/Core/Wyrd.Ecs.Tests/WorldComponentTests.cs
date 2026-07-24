namespace Wyrd.Ecs.Tests;

public class WorldComponentTests
{
    private struct Position : IComponent
    {
        public float X;
        public float Y;
    }

    private struct Velocity : IComponent
    {
        public float X;
    }

    [Fact]
    public void AddComponent_EntityThenHasIt()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.Commands.AddComponent(entity, new Position());
        world.ApplyCommands();

        world.HasComponent<Position>(entity).Should().BeTrue();
    }

    [Fact]
    public void AddComponent_StoresTheGivenValue()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.Commands.AddComponent(entity, new Position { X = 3f });
        world.ApplyCommands();

        world.GetComponent<Position>(entity).X.Should().Be(3f);
    }

    /// <summary>
    /// The {Position} archetype is created via GetOrCreateStorage (sized to match its
    /// own Entities array). Adding Velocity to the first such entity clones Position's
    /// storage into a brand-new {Position, Velocity} archetype instead — a different
    /// construction path that must size the clone the same way, or entities past a
    /// small hardcoded default would index out of range once this archetype grows.
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
        var entity = world.Commands.CreateEntity(new Position { X = 1f });
        world.Commands.AddComponent(entity, new Position { X = 2f });

        var act = () => world.ApplyCommands();

        act.Should().NotThrow();
        world.GetComponent<Position>(entity).X.Should().Be(2f);
    }

    [Fact]
    public void GetComponent_Missing_Throws()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        var act = () => world.GetComponent<Position>(entity);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TryGetComponent_Missing_ReturnsFalse()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.TryGetComponent<Position>(entity, out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetComponent_Present_ReturnsTrueAndValue()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity(new Position { X = 5f });
        world.ApplyCommands();

        world.TryGetComponent<Position>(entity, out var value).Should().BeTrue();
        value.X.Should().Be(5f);
    }

    [Fact]
    public void HasComponent_Missing_ReturnsFalse()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.HasComponent<Position>(entity).Should().BeFalse();
    }

    [Fact]
    public void RemoveComponent_Present_RemovesIt()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity(new Position());
        world.ApplyCommands();

        world.Commands.RemoveComponent<Position>(entity);
        world.ApplyCommands();

        world.HasComponent<Position>(entity).Should().BeFalse();
    }

    [Fact]
    public void RemoveComponent_Missing_IsANoOp()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.Commands.RemoveComponent<Position>(entity);
        var act = () => world.ApplyCommands();

        act.Should().NotThrow();
    }

    [Fact]
    public void ArchetypeMove_AddingASecondComponent_PreservesTheFirstOnesValue()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity(new Position { X = 11f });
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
        var entity = world.Commands.CreateEntity(new Position { X = 11f }, new Velocity { X = 22f });
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
        var a = world.Commands.CreateEntity(new Position { X = 1f });
        var b = world.Commands.CreateEntity(new Position { X = 2f });
        world.ApplyCommands();

        world.Commands.AddComponent(a, new Velocity());
        world.ApplyCommands();

        world.GetComponent<Position>(b).X.Should().Be(2f);
    }

    [Fact]
    public void ArchetypeMove_SharedTargetArchetype_BothEntitiesKeepTheirOwnValues()
    {
        var world = new World();
        var a = world.Commands.CreateEntity(new Position { X = 1f });
        var b = world.Commands.CreateEntity(new Position { X = 2f });
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
        var a = world.Commands.CreateEntity(new Position { X = 1f });
        var b = world.Commands.CreateEntity(new Position { X = 2f });
        world.ApplyCommands();

        world.Commands.DestroyEntity(a);
        world.ApplyCommands();

        world.GetComponent<Position>(b).X.Should().Be(2f);
    }
}
