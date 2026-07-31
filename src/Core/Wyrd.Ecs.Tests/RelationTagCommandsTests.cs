namespace Wyrd.Ecs.Tests;

public class RelationTagCommandsTests
{
    private struct Follows : ITag;

    [Fact]
    public void AddRelationTag_CreatesForwardAndBackwardLinks()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.Commands.AddRelationTag<Follows>(a, b);
        world.ApplyCommands();

        world.HasComponent<RelationTagLinks<Follows>>(a).Should().BeTrue();
        world.GetComponent<RelationTagLinks<Follows>>(a).Values.Should().Contain(b);
        world.HasComponent<RelationTagBacklinks<Follows>>(b).Should().BeTrue();
        world.GetComponent<RelationTagBacklinks<Follows>>(b).Values.Should().Contain(a);
    }

    [Fact]
    public void AddRelationTag_TwoDifferentTargets_BothPresentOnTheSameEntity()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        var c = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.Commands.AddRelationTag<Follows>(a, b);
        world.Commands.AddRelationTag<Follows>(a, c);
        world.ApplyCommands();

        world.GetComponent<RelationTagLinks<Follows>>(a).Values.Should().BeEquivalentTo([b, c]);
    }

    [Fact]
    public void AddRelationTag_SelfRelation_WorksWithoutSpecialCasing()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.Commands.AddRelationTag<Follows>(a, a);
        world.ApplyCommands();

        world.GetComponent<RelationTagLinks<Follows>>(a).Values.Should().Contain(a);
        world.GetComponent<RelationTagBacklinks<Follows>>(a).Values.Should().Contain(a);
    }

    [Fact]
    public void RemoveRelationTag_RemovesForwardAndBackwardLinks()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        world.Commands.AddRelationTag<Follows>(a, b);
        world.ApplyCommands();

        world.Commands.RemoveRelationTag<Follows>(a, b);
        world.ApplyCommands();

        world.HasComponent<RelationTagLinks<Follows>>(a).Should().BeFalse();
        world.HasComponent<RelationTagBacklinks<Follows>>(b).Should().BeFalse();
    }

    [Fact]
    public void RemoveRelationTag_EdgeThatNeverExisted_IsANoOp()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.Commands.RemoveRelationTag<Follows>(a, b);
        var act = () => world.ApplyCommands();

        act.Should().NotThrow();
    }

    [Fact]
    public void RelationTagBuffer_SharedAcrossRemoveRelationAndRelationTagCalls_DoesNotCrossWires()
    {
        // Regression guard for RelationTargetBuffer being shared across RemoveRelation<T>,
        // AddRelationTag<T>, and RemoveRelationTag<T> regardless of T (see Task 3's doc
        // comment on RelationTargetBuffer) -- queue several in the same batch and confirm
        // each one's captured (buffer, slot) still resolves to its own, correct target.
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        var c = world.Commands.CreateEntity();
        world.Commands.AddRelationTag<Follows>(a, b);
        world.ApplyCommands();

        world.Commands.AddRelationTag<Follows>(a, c);
        world.Commands.RemoveRelationTag<Follows>(a, b);
        world.ApplyCommands();

        world.GetComponent<RelationTagLinks<Follows>>(a).Values.Should().BeEquivalentTo([c]);
    }
}
