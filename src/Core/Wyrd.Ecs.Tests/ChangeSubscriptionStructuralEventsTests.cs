namespace Wyrd.Ecs.Tests;

public class ChangeSubscriptionStructuralEventsTests
{
    private struct Position : IComponent
    {
        public float X;
    }

    private struct Likes : IRelation;

    [Fact]
    public void StructuralEventsFalse_DoesNotReportEntityCreation()
    {
        var world = new World();
        using var subscription = world.Subscribe<Position>();

        world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();

        subscription.Drain().Should().BeEmpty();
    }

    [Fact]
    public void StructuralEventsTrue_ReportsEntityCreation()
    {
        var world = new World();
        using var subscription = world.Subscribe<Position>(structuralEvents: true);

        Entity a = world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();

        var entries = subscription.Drain();

        entries.Should().ContainSingle();
        entries[0].Entity.Should().Be(a);
        entries[0].Kind.Should().Be(ChangeKind.EntityCreated);
    }

    [Fact]
    public void StructuralEventsTrue_ReportsEntityDestruction()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();

        using var subscription = world.Subscribe<Position>(structuralEvents: true);
        world.Commands.DestroyEntity(a);
        world.ApplyCommands();

        var entries = subscription.Drain();

        entries.Should().ContainSingle();
        entries[0].Entity.Should().Be(a);
        entries[0].Kind.Should().Be(ChangeKind.EntityDestroyed);
    }

    /// <summary>
    /// The first edge added / last edge removed also moves RelationLinks&lt;T&gt;/
    /// RelationBacklinks&lt;T&gt; on/off the entity's archetype, which independently
    /// fires ComponentAdded/ComponentRemoved (see RelationQueryIntegrationTests.cs's own
    /// regression coverage for that contract) — a real, separately meaningful signal
    /// ("this entity's archetype/queryable shape changed"), not noise to suppress. This
    /// test only asserts the relation-specific events are present and correctly shaped,
    /// deliberately not asserting an exact total count.
    /// </summary>
    [Fact]
    public void StructuralEventsTrue_ReportsRelationLinkedThenUnlinked()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        world.ApplyCommands();

        using var subscription = world.Subscribe<Position>(structuralEvents: true);

        world.Commands.AddRelation<Likes>(a, b);
        world.ApplyCommands();
        world.Commands.RemoveRelation<Likes>(a, b);
        world.ApplyCommands();

        var entries = subscription.Drain();

        entries.Should().ContainSingle(e => e.Kind == ChangeKind.RelationLinked && e.Entity == a && e.Related == b);
        entries.Should().ContainSingle(e => e.Kind == ChangeKind.RelationUnlinked && e.Entity == a && e.Related == b);
    }

    [Fact]
    public void Dispose_StopsStructuralReporting()
    {
        var world = new World();
        var subscription = world.Subscribe<Position>(structuralEvents: true);
        subscription.Dispose();

        world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();

        var act = () => subscription.Drain();

        act.Should().Throw<KeyNotFoundException>();
    }
}
