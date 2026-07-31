namespace Wyrd.Ecs.Tests;

public class RelationCommandsTests
{
    private struct Likes : IComponent
    {
        public float Weight;
    }

    private struct Owns : IComponent;

    [Fact]
    public void AddRelation_CreatesForwardAndBackwardLinks()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.ApplyCommands();

        world.HasComponent<RelationLinks<Likes>>(a).Should().BeTrue();
        world.GetComponent<RelationLinks<Likes>>(a).Values[b].Weight.Should().Be(1f);
        world.HasComponent<RelationBacklinks<Likes>>(b).Should().BeTrue();
        world.GetComponent<RelationBacklinks<Likes>>(b).Values.Should().Contain(a);
    }

    [Fact]
    public void AddRelation_TwoDifferentTargets_BothPresentOnTheSameEntity()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        var c = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.Commands.AddRelation(a, c, new Likes { Weight = 2f });
        world.ApplyCommands();

        var values = world.GetComponent<RelationLinks<Likes>>(a).Values;
        values.Should().HaveCount(2);
        values[b].Weight.Should().Be(1f);
        values[c].Weight.Should().Be(2f);
    }

    [Fact]
    public void AddRelation_SameEdgeTwice_LastValueWins()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.Commands.AddRelation(a, b, new Likes { Weight = 9f });
        world.ApplyCommands();

        world.GetComponent<RelationLinks<Likes>>(a).Values[b].Weight.Should().Be(9f);
        world.GetComponent<RelationBacklinks<Likes>>(b).Values.Should().HaveCount(1); // still one backlink, not two
    }

    [Fact]
    public void AddRelation_SelfRelation_WorksWithoutSpecialCasing()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.Commands.AddRelation(a, a, new Likes { Weight = 5f });
        world.ApplyCommands();

        world.GetComponent<RelationLinks<Likes>>(a).Values[a].Weight.Should().Be(5f);
        world.GetComponent<RelationBacklinks<Likes>>(a).Values.Should().Contain(a);
    }

    [Fact]
    public void AddRelation_TargetNotAlive_IsANoOp()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        world.ApplyCommands();
        world.Commands.DestroyEntity(b);
        world.ApplyCommands();

        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        var act = () => world.ApplyCommands();

        act.Should().NotThrow();
        world.HasComponent<RelationLinks<Likes>>(a).Should().BeFalse();
    }

    [Fact]
    public void RemoveRelation_RemovesForwardAndBackwardLinks()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.ApplyCommands();

        world.Commands.RemoveRelation<Likes>(a, b);
        world.ApplyCommands();

        world.HasComponent<RelationLinks<Likes>>(a).Should().BeFalse(); // last edge removed -- component gone entirely
        world.HasComponent<RelationBacklinks<Likes>>(b).Should().BeFalse();
    }

    [Fact]
    public void RemoveRelation_OneOfManyTargets_LeavesTheOthersIntact()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        var c = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.Commands.AddRelation(a, c, new Likes { Weight = 2f });
        world.ApplyCommands();

        world.Commands.RemoveRelation<Likes>(a, b);
        world.ApplyCommands();

        world.HasComponent<RelationLinks<Likes>>(a).Should().BeTrue();
        var values = world.GetComponent<RelationLinks<Likes>>(a).Values;
        values.Should().NotContainKey(b);
        values[c].Weight.Should().Be(2f);
        world.HasComponent<RelationBacklinks<Likes>>(b).Should().BeFalse();
        world.HasComponent<RelationBacklinks<Likes>>(c).Should().BeTrue();
    }

    [Fact]
    public void RemoveRelation_EdgeThatNeverExisted_IsANoOp()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.Commands.RemoveRelation<Likes>(a, b);
        var act = () => world.ApplyCommands();

        act.Should().NotThrow();
    }

    [Fact]
    public void AddRelation_IsNotVisibleUntilApplied()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });

        world.HasComponent<RelationLinks<Likes>>(a).Should().BeFalse();
    }

    [Fact]
    public void AddRelation_ZeroSizedPayload_StillTracksTheEdge()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.Commands.AddRelation(a, b, new Owns());
        world.ApplyCommands();

        world.GetComponent<RelationLinks<Owns>>(a).Values.Should().ContainKey(b);
    }
}
