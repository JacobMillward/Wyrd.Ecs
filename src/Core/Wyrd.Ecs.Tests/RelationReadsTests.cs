namespace Wyrd.Ecs.Tests;

public class RelationReadsTests
{
    private struct Likes : IComponent
    {
        public float Weight;
    }

    private struct Follows : ITag;

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
    public void TryGetRelation_EdgePresent_ReturnsTrueAndTheValue()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, b, new Likes { Weight = 3f });
        world.ApplyCommands();

        var found = world.TryGetRelation<Likes>(a, b, out var value);

        found.Should().BeTrue();
        value.Weight.Should().Be(3f);
    }

    [Fact]
    public void TryGetRelation_EdgeAbsent_ReturnsFalse()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        world.ApplyCommands();

        var found = world.TryGetRelation<Likes>(a, b, out _);

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
    public void HasRelationTag_EdgePresent_ReturnsTrue()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        world.Commands.AddRelationTag<Follows>(a, b);
        world.ApplyCommands();

        world.HasRelationTag<Follows>(a, b).Should().BeTrue();
    }

    [Fact]
    public void TargetsTag_NoEdges_ReturnsEmpty()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.TargetsTag<Follows>(a).Should().BeEmpty();
    }

    [Fact]
    public void SourcesTag_ManySources_ReturnsAllOfThem()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        var target = world.Commands.CreateEntity();
        world.Commands.AddRelationTag<Follows>(a, target);
        world.Commands.AddRelationTag<Follows>(b, target);
        world.ApplyCommands();

        world.SourcesTag<Follows>(target).Should().BeEquivalentTo([a, b]);
    }
}
