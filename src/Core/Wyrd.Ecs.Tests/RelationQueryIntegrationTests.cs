using Likes = Wyrd.Ecs.Tests.RelationQueryIntegrationLikes;

namespace Wyrd.Ecs.Tests;

// File-scoped, not nested private: the query-chain generator emits code in a separate
// partial that can't see a private nested type -- matches QueryFluentBuilderTests.cs's
// existing convention for any component type used with .With<T>().ForEach(...).
struct RelationQueryIntegrationLikes : IComponent { public float Weight; }

public class RelationQueryIntegrationTests
{
    private sealed class RecordingObserver : IStructuralChangeObserver
    {
        internal readonly List<string> Events = new();

        public void OnEntityCreated(Entity entity) { }
        public void OnEntityDestroyed(Entity entity) { }
        public void OnComponentAdded(Entity entity, int typeIndex) => Events.Add($"Added:{entity.Id}:{typeIndex}");
        public void OnComponentRemoved(Entity entity, int typeIndex) => Events.Add($"Removed:{entity.Id}:{typeIndex}");
        public void OnTagAdded(Entity entity, int typeIndex) { }
        public void OnTagRemoved(Entity entity, int typeIndex) { }
    }

    [Fact]
    public void WithRelationLinks_MatchesEntitiesWithAtLeastOneEdge_RegardlessOfTarget()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        var c = world.Commands.CreateEntity();
        var untouched = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, c, new Likes { Weight = 1f }); // a -> c
        world.Commands.AddRelation(b, c, new Likes { Weight = 2f }); // b -> c, a different target than a's edge
        world.ApplyCommands();

        var count = 0;
        world.Query().With<RelationLinks<Likes>>()
            .ForEach(0, (in int _, in RelationLinks<Likes> link) => count++);

        count.Should().Be(2); // a and b, both matched purely by "has any Likes edge" -- untouched and c (which only has backlinks) are excluded
    }

    [Fact]
    public void AddRelation_MultipleEdgesOnTheSameEntity_NotifiesComponentAddedOnlyOnce()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        var c = world.Commands.CreateEntity();
        world.ApplyCommands();
        var observer = new RecordingObserver();
        using var subscription = world.ObserveStructuralChanges(observer);

        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.Commands.AddRelation(a, c, new Likes { Weight = 2f }); // second edge on `a`, same relation type -- no archetype move
        world.ApplyCommands();

        var addedForA = observer.Events.Count(e => e.StartsWith($"Added:{a.Id}:"));
        addedForA.Should().Be(1);
    }

    [Fact]
    public void RemoveRelation_LastEdge_NotifiesComponentRemoved()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.ApplyCommands();
        var observer = new RecordingObserver();
        using var subscription = world.ObserveStructuralChanges(observer);

        world.Commands.RemoveRelation<Likes>(a, b);
        world.ApplyCommands();

        observer.Events.Should().Contain(e => e.StartsWith($"Removed:{a.Id}:"));
    }

    [Fact]
    public void RemoveRelation_OneOfTwoEdges_DoesNotNotifyComponentRemoved()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        var c = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.Commands.AddRelation(a, c, new Likes { Weight = 2f });
        world.ApplyCommands();
        var observer = new RecordingObserver();
        using var subscription = world.ObserveStructuralChanges(observer);

        world.Commands.RemoveRelation<Likes>(a, b); // one of two -- RelationLinks<Likes> itself survives

        world.ApplyCommands();

        observer.Events.Should().NotContain(e => e.StartsWith($"Removed:{a.Id}:"));
    }
}
