namespace Wyrd.Ecs.Tests;

public class RelationExclusivityTests
{
    private struct Parent : IExclusiveRelation
    {
        public float Weight;
    }

    private struct Likes : IRelation
    {
        public float Weight;
    }

    [Fact]
    public void AddRelation_ExclusiveType_SecondTarget_ReplacesTheFirst()
    {
        var world = new World();
        var child = world.Commands.CreateEntity();
        var momOne = world.Commands.CreateEntity();
        var momTwo = world.Commands.CreateEntity();
        world.Commands.AddRelation(child, momOne, new Parent { Weight = 1f });
        world.ApplyCommands();

        world.Commands.AddRelation(child, momTwo, new Parent { Weight = 2f });
        world.ApplyCommands();

        world.Targets<Parent>(child).Should().HaveCount(1);
        world.Targets<Parent>(child).Should().ContainKey(momTwo);
        world.HasRelation<Parent>(child, momOne).Should().BeFalse();
    }

    [Fact]
    public void AddRelation_ExclusiveType_ReplacingTarget_RemovesTheOldTargetsBacklink()
    {
        var world = new World();
        var child = world.Commands.CreateEntity();
        var momOne = world.Commands.CreateEntity();
        var momTwo = world.Commands.CreateEntity();
        world.Commands.AddRelation(child, momOne, new Parent { Weight = 1f });
        world.ApplyCommands();

        world.Commands.AddRelation(child, momTwo, new Parent { Weight = 2f });
        world.ApplyCommands();

        world.HasComponent<RelationBacklinks<Parent>>(momOne).Should().BeFalse();
        world.GetComponent<RelationBacklinks<Parent>>(momTwo).Values.Should().Contain(child);
    }

    [Fact]
    public void AddRelation_ExclusiveType_SameTargetAgain_OverwritesTheValueWithoutTouchingTheBacklink()
    {
        var world = new World();
        var child = world.Commands.CreateEntity();
        var mom = world.Commands.CreateEntity();
        world.Commands.AddRelation(child, mom, new Parent { Weight = 1f });
        world.ApplyCommands();

        world.Commands.AddRelation(child, mom, new Parent { Weight = 9f });
        world.ApplyCommands();

        world.Targets<Parent>(child).Should().HaveCount(1);
        world.GetComponent<RelationLinks<Parent>>(child).Values[mom].Weight.Should().Be(9f);
        world.GetComponent<RelationBacklinks<Parent>>(mom).Values.Should().HaveCount(1);
    }

    [Fact]
    public void AddRelation_ExclusiveType_FirstEverEdge_WorksLikeAnyOtherRelation()
    {
        var world = new World();
        var child = world.Commands.CreateEntity();
        var mom = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.Commands.AddRelation(child, mom, new Parent { Weight = 1f });
        world.ApplyCommands();

        world.Targets<Parent>(child).Should().ContainKey(mom);
    }

    [Fact]
    public void AddRelation_ExclusiveType_ReplacingASelfTarget_WorksWithoutCorruption()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, a, new Parent { Weight = 1f }); // a is initially its own parent
        world.ApplyCommands();

        var act = () => { world.Commands.AddRelation(a, b, new Parent { Weight = 2f }); world.ApplyCommands(); };

        act.Should().NotThrow();
        world.Targets<Parent>(a).Should().HaveCount(1);
        world.Targets<Parent>(a).Should().ContainKey(b);
        world.HasComponent<RelationBacklinks<Parent>>(a).Should().BeFalse(); // a is no longer its own target, so its backlinks component is gone
    }

    [Fact]
    public void AddRelation_NonExclusiveType_SecondTarget_IsAddedAlongsideTheFirst()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        var c = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.ApplyCommands();

        world.Commands.AddRelation(a, c, new Likes { Weight = 2f });
        world.ApplyCommands();

        world.Targets<Likes>(a).Should().HaveCount(2); // Likes is not IExclusiveRelation -- both targets coexist
    }
}
