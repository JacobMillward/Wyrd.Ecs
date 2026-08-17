namespace Wyrd.Ecs.Tests;

public struct Score : IResource { public int Value; }

public class WorldResourceTests
{
    [Fact]
    public void AddResource_ThenGetResource_ReturnsTheRegisteredValue()
    {
        var world = new WorldBuilder().Build();

        world.AddResource(new Score { Value = 5 });

        world.GetResource<Score>().Value.Should().Be(5);
    }

    [Fact]
    public void AddResourceWithFactory_ReceivesTheWorldAndStoresTheResult()
    {
        var world = new WorldBuilder().Build();

        world.AddResource(w => new Score { Value = ReferenceEquals(w, world) ? 42 : -1 });

        world.GetResource<Score>().Value.Should().Be(42);
    }

    [Fact]
    public void AddResource_CalledTwiceForTheSameType_Throws()
    {
        var world = new WorldBuilder().Build();
        world.AddResource(new Score { Value = 1 });

        var act = () => world.AddResource(new Score { Value = 2 });

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetResource_WhenNotRegistered_Throws()
    {
        var world = new WorldBuilder().Build();

        var act = () => world.GetResource<Score>();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TryGetResource_WhenNotRegistered_ReturnsFalse()
    {
        var world = new WorldBuilder().Build();

        var found = world.TryGetResource<Score>(out _);

        found.Should().BeFalse();
    }

    [Fact]
    public void TryGetResource_WhenRegistered_ReturnsTrueAndTheValue()
    {
        var world = new WorldBuilder().Build();
        world.AddResource(new Score { Value = 7 });

        var found = world.TryGetResource<Score>(out var score);

        found.Should().BeTrue();
        score.Value.Should().Be(7);
    }

    [Fact]
    public void GetResourceRef_MutatingThroughIt_IsVisibleToASubsequentGetResource()
    {
        var world = new WorldBuilder().Build();
        world.AddResource(new Score { Value = 1 });

        world.GetResourceRef<Score>().Value = 99;

        world.GetResource<Score>().Value.Should().Be(99);
    }

    [Fact]
    public void RemoveResource_ThenGetResource_Throws()
    {
        var world = new WorldBuilder().Build();
        world.AddResource(new Score { Value = 1 });

        var removed = world.RemoveResource<Score>();

        removed.Should().BeTrue();
        var act = () => world.GetResource<Score>();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RemoveResource_WhenNotRegistered_ReturnsFalse()
    {
        var world = new WorldBuilder().Build();

        world.RemoveResource<Score>().Should().BeFalse();
    }

    [Fact]
    public void RemoveResource_ThenAddResourceAgain_Succeeds()
    {
        var world = new WorldBuilder().Build();
        world.AddResource(new Score { Value = 1 });
        world.RemoveResource<Score>();

        world.AddResource(new Score { Value = 2 });

        world.GetResource<Score>().Value.Should().Be(2);
    }

    [Fact]
    public void TwoWorlds_HaveIndependentResourceStorage()
    {
        var worldA = new WorldBuilder().Build();
        var worldB = new WorldBuilder().Build();
        worldA.AddResource(new Score { Value = 1 });
        worldB.AddResource(new Score { Value = 2 });

        worldA.GetResourceRef<Score>().Value = 100;

        worldA.GetResource<Score>().Value.Should().Be(100);
        worldB.GetResource<Score>().Value.Should().Be(2);
    }
}
