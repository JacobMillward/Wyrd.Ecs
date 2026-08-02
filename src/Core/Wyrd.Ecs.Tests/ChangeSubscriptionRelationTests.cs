namespace Wyrd.Ecs.Tests;

public class ChangeSubscriptionRelationTests
{
    private struct Position : IComponent
    {
        public float X;
    }

    private struct Likes : IRelation
    {
        public float Weight;
    }

    private struct Owns : IRelation;

    [Fact]
    public void SubscribeRelation_ReportsALinkForItsOwnType()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        world.ApplyCommands();

        using var subscription = world.SubscribeRelation<Likes>();

        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.ApplyCommands();

        var entries = subscription.Drain();

        entries.Should().ContainSingle();
        entries[0].Entity.Should().Be(a);
        entries[0].Related.Should().Be(b);
        entries[0].Kind.Should().Be(ChangeKind.RelationLinked);
        entries[0].TypeIndex.Should().Be(Wyrd.Ecs.Internal.TypeIndex<Likes>.Value);
    }

    [Fact]
    public void SubscribeRelation_ReportsAnUnlinkForItsOwnType()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.ApplyCommands();

        using var subscription = world.SubscribeRelation<Likes>();

        world.Commands.RemoveRelation<Likes>(a, b);
        world.ApplyCommands();

        var entries = subscription.Drain();

        entries.Should().ContainSingle();
        entries[0].Kind.Should().Be(ChangeKind.RelationUnlinked);
    }

    [Fact]
    public void SubscribeRelation_DoesNotReportADifferentRelationTypesLink()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        world.ApplyCommands();

        using var subscription = world.SubscribeRelation<Likes>();

        world.Commands.AddRelation<Owns>(a, b);
        world.ApplyCommands();

        subscription.Drain().Should().BeEmpty();
    }

    [Fact]
    public void SubscribeRelation_DoesNotReportUnrelatedStructuralEvents()
    {
        var world = new World();
        using var subscription = world.SubscribeRelation<Likes>();

        world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();

        subscription.Drain().Should().BeEmpty();
    }

    [Fact]
    public void SubscribeRelation_DoesNotReportAPlainComponentValueChange()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();

        using var subscription = world.SubscribeRelation<Likes>();

        world.GetComponent<Position>(a).X = 2f;
        world.AdvanceTick();

        subscription.Drain().Should().BeEmpty();
    }

    [Fact]
    public void Dispose_StopsFurtherRelationReporting()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        world.ApplyCommands();

        var subscription = world.SubscribeRelation<Likes>();
        subscription.Dispose();

        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.ApplyCommands();

        var act = () => subscription.Drain();

        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void TwoIndependentSubscribeRelationCalls_ForDifferentTypes_OnlyReceiveTheirOwnEdges()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        world.ApplyCommands();

        using var likes = world.SubscribeRelation<Likes>();
        using var owns = world.SubscribeRelation<Owns>();

        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.Commands.AddRelation<Owns>(a, b);
        world.ApplyCommands();

        likes.Drain().Should().ContainSingle(e => e.TypeIndex == Wyrd.Ecs.Internal.TypeIndex<Likes>.Value);
        owns.Drain().Should().ContainSingle(e => e.TypeIndex == Wyrd.Ecs.Internal.TypeIndex<Owns>.Value);
    }
}
