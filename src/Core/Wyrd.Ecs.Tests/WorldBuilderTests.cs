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
        Entity entity = world.Commands.CreateEntity(new Position { X = 5f });
        world.ApplyCommands();

        world.GetComponent<Position>(entity).X.Should().Be(5f);
    }

    [Fact]
    public void Build_TracksNothingByDefault_SameAsPlainWorld()
    {
        var world = new WorldBuilder().Build();
        Entity entity = world.Commands.CreateEntity(new Position());
        world.ApplyCommands();
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
        Entity entity = world.Commands.CreateEntity(new Position());
        world.ApplyCommands();

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

    [Fact]
    public void OnBuilt_IsInvokedOnceWithTheConstructedWorld_AfterBuildReturns()
    {
        var builder = new WorldBuilder();
        World? received = null;
        builder.OnBuilt += w => received = w;

        var world = builder.Build();

        received.Should().BeSameAs(world);
    }

    [Fact]
    public void OnBuilt_WithMultipleSubscribers_InvokesAllOfThemInSubscriptionOrder()
    {
        var builder = new WorldBuilder();
        var order = new List<int>();
        builder.OnBuilt += _ => order.Add(1);
        builder.OnBuilt += _ => order.Add(2);

        builder.Build();

        order.Should().Equal(1, 2);
    }

    [Fact]
    public void OnBuilt_WithNoSubscribers_DoesNotThrow()
    {
        var act = () => new WorldBuilder().Build();

        act.Should().NotThrow();
    }
}
