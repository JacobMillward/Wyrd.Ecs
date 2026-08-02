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

    private sealed class ComponentChangeCountingObserver : IStructuralChangeObserver
    {
        internal int AddedCount;
        internal int RemovedCount;

        public void OnEntityCreated(Entity entity) { }
        public void OnEntityDestroyed(Entity entity) { }
        public void OnComponentAdded(Entity entity, int typeIndex) => AddedCount++;
        public void OnComponentRemoved(Entity entity, int typeIndex) => RemovedCount++;
        public void OnTagAdded(Entity entity, int typeIndex) { }
        public void OnTagRemoved(Entity entity, int typeIndex) { }
    }

    [Fact]
    public void AddRelation_ExclusiveType_SecondTarget_ReplacesTheFirst()
    {
        var world = new World();
        Entity child = world.Commands.CreateEntity();
        Entity momOne = world.Commands.CreateEntity();
        Entity momTwo = world.Commands.CreateEntity();
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
        Entity child = world.Commands.CreateEntity();
        Entity momOne = world.Commands.CreateEntity();
        Entity momTwo = world.Commands.CreateEntity();
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
        Entity child = world.Commands.CreateEntity();
        Entity mom = world.Commands.CreateEntity();
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
        Entity child = world.Commands.CreateEntity();
        Entity mom = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.Commands.AddRelation(child, mom, new Parent { Weight = 1f });
        world.ApplyCommands();

        world.Targets<Parent>(child).Should().ContainKey(mom);
    }

    [Fact]
    public void AddRelation_ExclusiveType_ReplacingASelfTarget_WorksWithoutCorruption()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, a, new Parent { Weight = 1f }); // a is initially its own parent
        world.ApplyCommands();

        var act = () => { world.Commands.AddRelation(a, b, new Parent { Weight = 2f }); world.ApplyCommands(); };

        act.Should().NotThrow();
        world.Targets<Parent>(a).Should().HaveCount(1);
        world.Targets<Parent>(a).Should().ContainKey(b);
        world.HasComponent<RelationBacklinks<Parent>>(a).Should().BeFalse("a is no longer its own target, so its backlinks component is gone");
    }

    [Fact]
    public void AddRelation_ExclusiveType_ReplacingTheOnlyTarget_NeverMovesTheSourcesRelationLinksComponent()
    {
        // Replacing an exclusive relation's sole target must mutate RelationLinks<Parent> in
        // place, never remove-then-re-add it: no ComponentAdded/ComponentRemoved should fire
        // for `child`.
        var world = new World();
        Entity child = world.Commands.CreateEntity();
        Entity momOne = world.Commands.CreateEntity();
        Entity momTwo = world.Commands.CreateEntity();
        world.Commands.AddRelation(child, momOne, new Parent { Weight = 1f });
        world.ApplyCommands();
        var observer = new ComponentChangeCountingObserver();
        using var subscription = world.ObserveStructuralChanges(observer);

        world.Commands.AddRelation(child, momTwo, new Parent { Weight = 2f });
        world.ApplyCommands();

        observer.AddedCount.Should().Be(1, "only momOne's RelationBacklinks<Parent> removal and momTwo's add should count; if child's own RelationLinks<Parent> had also been removed-then-re-added, these counts would be 2 and 2");
        observer.RemovedCount.Should().Be(1, "only momOne's RelationBacklinks<Parent> removal and momTwo's add should count; if child's own RelationLinks<Parent> had also been removed-then-re-added, these counts would be 2 and 2");
    }

    [Fact]
    public void AddRelation_NonExclusiveType_SecondTarget_IsAddedAlongsideTheFirst()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        Entity c = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.ApplyCommands();

        world.Commands.AddRelation(a, c, new Likes { Weight = 2f });
        world.ApplyCommands();

        world.Targets<Likes>(a).Should().HaveCount(2, "Likes is not IExclusiveRelation, so both targets coexist");
    }
}
