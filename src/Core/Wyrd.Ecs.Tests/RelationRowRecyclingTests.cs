namespace Wyrd.Ecs.Tests;

/// <summary>
/// Relation-link components initialize their edge dictionary on first use by branching on
/// the storage slot reading default - so a recycled archetype row must never surface a prior
/// occupant's bytes, or a new entity would silently inherit a destroyed entity's edges.
/// </summary>
public class RelationRowRecyclingTests
{
    private struct Likes : IRelation;

    [Fact]
    public void RelationComponentOnRecycledRow_StartsWithNoEdges()
    {
        var world = new World();
        Entity originalHolder = world.Commands.CreateEntity();
        var firstTarget = world.Commands.CreateEntity();
        var secondTarget = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.Commands.AddRelation<Likes>(originalHolder, firstTarget);
        world.ApplyCommands();

        // Destroying the sole holder vacates its archetype's tail slots with no backfill;
        // recycling must hand the next holder a default-initialized slot.
        world.Commands.DestroyEntity(originalHolder);
        world.ApplyCommands();

        Entity recycledHolder = world.Commands.CreateEntity();
        world.ApplyCommands();

        recycledHolder.Id.Should().Be(originalHolder.Id, "the id - and its storage row - must actually recycle for this test to exercise anything");
        // A DIFFERENT target than the previous incarnation's edge: an inherited dictionary
        // would surface both entries, so exactly-one discriminates cleanly.
        world.Commands.AddRelation<Likes>(recycledHolder, secondTarget);
        world.ApplyCommands();

        var targets = world.Targets<Likes>(recycledHolder);
        targets.Keys.Should().ContainSingle().Which.Should().Be(secondTarget);
    }
}
