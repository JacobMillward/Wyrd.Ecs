namespace Wyrd.Ecs.Tests;

public class RelationReadsTests
{
    private struct Likes : IRelation
    {
        public float Weight;
    }

    private struct Follows : IRelation;

    [Fact]
    public void HasRelation_EdgePresent_ReturnsTrue()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.ApplyCommands();

        world.HasRelation<Likes>(a, b).Should().BeTrue();
    }

    [Fact]
    public void HasRelation_EdgeAbsent_ReturnsFalse()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.HasRelation<Likes>(a, b).Should().BeFalse();
    }

    [Fact]
    public void TryGetRelation_EdgePresent_ReturnsFoundAndTheTrackedValue()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, b, new Likes { Weight = 3f });
        world.ApplyCommands();

        ref var value = ref world.TryGetRelation<Likes>(a, b, out var found);

        found.Should().BeTrue();
        value.Weight.Should().Be(3f);
    }

    [Fact]
    public void TryGetRelation_EdgePresent_ReturnedRefWritesThrough()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, b, new Likes { Weight = 3f });
        world.ApplyCommands();

        ref var value = ref world.TryGetRelation<Likes>(a, b, out _);
        value.Weight = 8f;

        world.TryGetRelation<Likes>(a, b, out _).Weight.Should().Be(8f);
    }

    [Fact]
    public void TryGetRelation_EdgeAbsent_ReturnsNotFound()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.TryGetRelation<Likes>(a, b, out var found);

        found.Should().BeFalse();
    }

    [Fact]
    public void Targets_NoEdges_ReturnsEmpty()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.Targets<Likes>(a).Should().BeEmpty();
    }

    [Fact]
    public void Targets_ManyEdges_ReturnsAllOfThem()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        var c = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.Commands.AddRelation(a, c, new Likes { Weight = 2f });
        world.ApplyCommands();

        world.Targets<Likes>(a).Should().HaveCount(2);
    }

    [Fact]
    public void Sources_ManySources_ReturnsAllOfThem()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        var target = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, target, new Likes { Weight = 1f });
        world.Commands.AddRelation(b, target, new Likes { Weight = 2f });
        world.ApplyCommands();

        world.Sources<Likes>(target).Should().BeEquivalentTo([a, b]);
    }

    [Fact]
    public void HasRelation_MarkerOnlyRelationType_EdgePresent_ReturnsTrue()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        world.Commands.AddRelation<Follows>(a, b);
        world.ApplyCommands();

        world.HasRelation<Follows>(a, b).Should().BeTrue();
    }

    [Fact]
    public void Targets_MarkerOnlyRelationType_NoEdges_ReturnsEmpty()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.Targets<Follows>(a).Should().BeEmpty();
    }

    [Fact]
    public void Sources_MarkerOnlyRelationType_ManySources_ReturnsAllOfThem()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        var target = world.Commands.CreateEntity();
        world.Commands.AddRelation<Follows>(a, target);
        world.Commands.AddRelation<Follows>(b, target);
        world.ApplyCommands();

        world.Sources<Follows>(target).Should().BeEquivalentTo([a, b]);
    }
}
