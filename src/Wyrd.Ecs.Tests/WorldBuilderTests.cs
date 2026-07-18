namespace Wyrd.Ecs.Tests;

public class WorldBuilderTests
{
    private struct Position : IComponent
    {
        public float X;
    }

    [Fact]
    public void Build_ProducesAWorkingWorld()
    {
        var world = new WorldBuilder().Build();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity).X = 5f;

        world.GetComponent<Position>(entity).X.Should().Be(5f);
    }

    [Fact]
    public void Build_TracksNothingByDefault_SameAsPlainWorld()
    {
        var world = new WorldBuilder().Build();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity);
        world.AdvanceTick();

        world.GetComponent<Position>(entity).X += 1f;

        var (archetype, row) = TestReflection.GetLocation(world, entity);
        var storage = archetype.Storages[Wyrd.Ecs.Internal.TypeIndex<Position>.Value];
        storage.RawLastMarkedTick[row].Should().NotBe(world.CurrentTick);
    }

    [Fact]
    public void WithArchetypeCapacity_SizesEveryArchetypesEntityArray()
    {
        var world = new WorldBuilder().WithArchetypeCapacity(16).Build();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity);

        var (archetype, _) = TestReflection.GetLocation(world, entity);

        archetype.Entities.Length.Should().Be(16);
    }

    [Fact]
    public void WithArchetypeCapacity_NonPositive_Throws()
    {
        var builder = new WorldBuilder();

        var act = () => builder.WithArchetypeCapacity(0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
