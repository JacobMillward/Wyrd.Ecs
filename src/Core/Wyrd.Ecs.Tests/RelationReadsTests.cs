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
    public void GetRelation_EdgePresent_ReturnsATrackedReferenceToThePayload()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, b, new Likes { Weight = 4f });
        world.ApplyCommands();

        world.GetRelation<Likes>(a, b).Weight.Should().Be(4f);
    }

    [Fact]
    public void GetRelation_EdgePresent_ReturnedRefWritesThrough()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, b, new Likes { Weight = 4f });
        world.ApplyCommands();

        world.GetRelation<Likes>(a, b).Weight = 7f;

        world.TryGetRelation<Likes>(a, b, out _).Weight.Should().Be(7f);
    }

    [Fact]
    public void GetRelation_SourceHasNoEdgesOfThisType_Throws()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        world.ApplyCommands();

        var act = () => world.GetRelation<Likes>(a, b);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetRelation_SourceHasOtherEdgesButNotToThisTarget_Throws()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        var c = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.ApplyCommands();

        var act = () => world.GetRelation<Likes>(a, c);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetRelation_TargetNotFound_DoesNotMarkTheRowDirty()
    {
        var world = new World();
        using var tracking = world.TrackChanges<RelationLinks<Likes>>();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        var c = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, b, new Likes { Weight = 1f }); // gives a a RelationLinks<Likes> row
        world.ApplyCommands();
        world.AdvanceTick();

        var act = () => world.GetRelation<Likes>(a, c); // b exists, c doesn't -- must throw, not mark dirty
        act.Should().Throw<InvalidOperationException>();

        var (archetype, row) = TestReflection.GetLocation(world, a);
        var storage = archetype.Storages[Wyrd.Ecs.Internal.TypeIndex<RelationLinks<Likes>>.Value];
        storage.RawLastMarkedTick[row].Should().NotBe(world.CurrentTick);
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
