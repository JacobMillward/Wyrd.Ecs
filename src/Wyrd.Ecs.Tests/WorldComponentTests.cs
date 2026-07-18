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
        var entity = world.CreateEntity();

        world.AddComponent<Position>(entity);

        world.HasComponent<Position>(entity).Should().BeTrue();
    }

    [Fact]
    public void AddComponent_ReturnsAWritableReference()
    {
        var world = new World();
        var entity = world.CreateEntity();

        ref var position = ref world.AddComponent<Position>(entity);
        position.X = 3f;

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
        var seed = world.CreateEntity();
        world.AddComponent<Position>(seed);

        var entities = new Entity[10];
        for (var i = 0; i < entities.Length; i++)
        {
            entities[i] = world.CreateEntity();
            world.AddComponent<Position>(entities[i]).X = i;
            world.AddComponent<Velocity>(entities[i]);
        }

        for (var i = 0; i < entities.Length; i++)
            world.GetComponent<Position>(entities[i]).X.Should().Be(i);
    }

    [Fact]
    public void AddComponent_Twice_Throws()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity);

        var act = () => world.AddComponent<Position>(entity);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetComponent_Missing_Throws()
    {
        var world = new World();
        var entity = world.CreateEntity();

        var act = () => world.GetComponent<Position>(entity);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TryGetComponent_Missing_ReturnsFalse()
    {
        var world = new World();
        var entity = world.CreateEntity();

        world.TryGetComponent<Position>(entity, out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetComponent_Present_ReturnsTrueAndValue()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity).X = 5f;

        world.TryGetComponent<Position>(entity, out var value).Should().BeTrue();
        value.X.Should().Be(5f);
    }

    [Fact]
    public void HasComponent_Missing_ReturnsFalse()
    {
        var world = new World();
        var entity = world.CreateEntity();

        world.HasComponent<Position>(entity).Should().BeFalse();
    }

    [Fact]
    public void RemoveComponent_Present_RemovesIt()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity);

        world.RemoveComponent<Position>(entity);

        world.HasComponent<Position>(entity).Should().BeFalse();
    }

    [Fact]
    public void RemoveComponent_Missing_IsANoOp()
    {
        var world = new World();
        var entity = world.CreateEntity();

        var act = () => world.RemoveComponent<Position>(entity);

        act.Should().NotThrow();
    }

    [Fact]
    public void ArchetypeMove_AddingASecondComponent_PreservesTheFirstOnesValue()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity).X = 11f;

        world.AddComponent<Velocity>(entity).X = 22f;

        world.GetComponent<Position>(entity).X.Should().Be(11f);
        world.GetComponent<Velocity>(entity).X.Should().Be(22f);
    }

    [Fact]
    public void ArchetypeMove_RemovingOneComponent_PreservesTheOthersValue()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity).X = 11f;
        world.AddComponent<Velocity>(entity).X = 22f;

        world.RemoveComponent<Position>(entity);

        world.HasComponent<Position>(entity).Should().BeFalse();
        world.GetComponent<Velocity>(entity).X.Should().Be(22f);
    }

    [Fact]
    public void ArchetypeMove_DoesNotDisturbOtherEntitiesInTheSourceArchetype()
    {
        var world = new World();
        var a = world.CreateEntity();
        var b = world.CreateEntity();
        world.AddComponent<Position>(a).X = 1f;
        world.AddComponent<Position>(b).X = 2f;

        world.AddComponent<Velocity>(a);

        world.GetComponent<Position>(b).X.Should().Be(2f);
    }

    [Fact]
    public void ArchetypeMove_SharedTargetArchetype_BothEntitiesKeepTheirOwnValues()
    {
        var world = new World();
        var a = world.CreateEntity();
        var b = world.CreateEntity();
        world.AddComponent<Position>(a).X = 1f;
        world.AddComponent<Position>(b).X = 2f;

        world.AddComponent<Velocity>(a).X = 10f;
        world.AddComponent<Velocity>(b).X = 20f;

        world.GetComponent<Position>(a).X.Should().Be(1f);
        world.GetComponent<Velocity>(a).X.Should().Be(10f);
        world.GetComponent<Position>(b).X.Should().Be(2f);
        world.GetComponent<Velocity>(b).X.Should().Be(20f);
    }

    [Fact]
    public void DestroyEntity_WithComponents_DoesNotCorruptSurvivingEntity()
    {
        var world = new World();
        var a = world.CreateEntity();
        var b = world.CreateEntity();
        world.AddComponent<Position>(a).X = 1f;
        world.AddComponent<Position>(b).X = 2f;

        world.DestroyEntity(a);

        world.GetComponent<Position>(b).X.Should().Be(2f);
    }
}
