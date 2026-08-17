namespace Wyrd.Ecs.Tests;

public class WorldBuilderResourceTests
{
    [Fact]
    public void AddResource_IsAvailableOnTheBuiltWorld()
    {
        var world = new WorldBuilder().AddResource(new Score { Value = 3 }).Build();

        world.GetResource<Score>().Value.Should().Be(3);
    }

    [Fact]
    public void AddResourceWithFactory_IsAvailableOnTheBuiltWorld()
    {
        var world = new WorldBuilder().AddResource(_ => new Score { Value = 9 }).Build();

        world.GetResource<Score>().Value.Should().Be(9);
    }

    [Fact]
    public void AddResource_CalledTwiceOnTheSameBuilder_ThrowsImmediately()
    {
        var builder = new WorldBuilder().AddResource(new Score { Value = 1 });

        var act = () => builder.AddResource(new Score { Value = 2 });

        act.Should().Throw<InvalidOperationException>();
    }
}
