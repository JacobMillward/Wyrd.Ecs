namespace Wyrd.Ecs.Tests;

public class EntityViewTests
{
    private struct Position : IComponent
    {
        public float X;
    }

    private struct Flag : ITag;

    private struct Likes : IRelation
    {
        public float Weight;
    }

    private struct Follows : IRelation;

    [Fact]
    public void Entity_ReturnsTheBoundEntity()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        world[entity].Entity.Should().Be(entity);
    }

    [Fact]
    public void GetComponent_ReturnsATrackedReferenceToTheValue()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity(new Position { X = 5f });
        world.ApplyCommands();

        world[entity].GetComponent<Position>().X.Should().Be(5f);
    }

    [Fact]
    public void TryGetComponent_Missing_ReturnsNotFound()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        world[entity].TryGetComponent<Position>(out var found);

        found.Should().BeFalse();
    }

    [Fact]
    public void TryGetComponent_Present_ReturnsFoundAndTheTrackedValue()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity(new Position { X = 5f });
        world.ApplyCommands();

        ref var value = ref world[entity].TryGetComponent<Position>(out var found);

        found.Should().BeTrue();
        value.X.Should().Be(5f);
    }

    [Fact]
    public void HasComponent_Present_ReturnsTrue()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity(new Position());
        world.ApplyCommands();

        world[entity].HasComponent<Position>().Should().BeTrue();
    }

    [Fact]
    public void HasComponent_Missing_ReturnsFalse()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        world[entity].HasComponent<Position>().Should().BeFalse();
    }

    [Fact]
    public void AddComponent_QueuesTheAdd_VisibleAfterApplyCommands()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        world[entity].AddComponent(new Position { X = 3f });
        world.HasComponent<Position>(entity).Should().BeFalse(); // still deferred
        world.ApplyCommands();

        world.GetComponent<Position>(entity).X.Should().Be(3f);
    }

    [Fact]
    public void RemoveComponent_QueuesTheRemove_VisibleAfterApplyCommands()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity(new Position());
        world.ApplyCommands();

        world[entity].RemoveComponent<Position>();
        world.ApplyCommands();

        world.HasComponent<Position>(entity).Should().BeFalse();
    }

    [Fact]
    public void AddComponent_ReturnsTheSameViewForChaining()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        var view = world[entity].AddComponent(new Position { X = 1f });

        view.Entity.Should().Be(entity);
    }

    [Fact]
    public void HasTag_Present_ReturnsTrue()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity();
        world.Commands.AddTag<Flag>(entity);
        world.ApplyCommands();

        world[entity].HasTag<Flag>().Should().BeTrue();
    }

    [Fact]
    public void HasTag_Missing_ReturnsFalse()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        world[entity].HasTag<Flag>().Should().BeFalse();
    }

    [Fact]
    public void AddTag_QueuesTheAdd_VisibleAfterApplyCommands()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        world[entity].AddTag<Flag>();
        world.ApplyCommands();

        world.HasTag<Flag>(entity).Should().BeTrue();
    }

    [Fact]
    public void RemoveTag_QueuesTheRemove_VisibleAfterApplyCommands()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity();
        world.Commands.AddTag<Flag>(entity);
        world.ApplyCommands();

        world[entity].RemoveTag<Flag>();
        world.ApplyCommands();

        world.HasTag<Flag>(entity).Should().BeFalse();
    }

    [Fact]
    public void HasRelation_EdgePresent_ReturnsTrue()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.ApplyCommands();

        world[a].HasRelation<Likes>(b).Should().BeTrue();
    }

    [Fact]
    public void HasRelation_EdgeAbsent_ReturnsFalse()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        world.ApplyCommands();

        world[a].HasRelation<Likes>(b).Should().BeFalse();
    }

    [Fact]
    public void GetRelation_EdgePresent_ReturnsATrackedReferenceToThePayload()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.ApplyCommands();

        world[a].GetRelation<Likes>(b).Weight.Should().Be(1f);
    }

    [Fact]
    public void GetRelation_EdgePresent_ReturnedRefWritesThrough()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.ApplyCommands();

        world[a].GetRelation<Likes>(b).Weight = 9f;

        world.TryGetRelation<Likes>(a, b, out _).Weight.Should().Be(9f);
    }

    [Fact]
    public void GetRelation_SourceHasNoEdgesOfThisType_Throws()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        world.ApplyCommands();

        var act = () => world[a].GetRelation<Likes>(b);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TryGetRelation_EdgePresent_ReturnsFoundAndTheTrackedValue()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.ApplyCommands();

        ref var value = ref world[a].TryGetRelation<Likes>(b, out var found);

        found.Should().BeTrue();
        value.Weight.Should().Be(1f);
    }

    [Fact]
    public void TryGetRelation_EdgeAbsent_ReturnsNotFound()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        world.ApplyCommands();

        world[a].TryGetRelation<Likes>(b, out var found);

        found.Should().BeFalse();
    }

    [Fact]
    public void AddRelation_WithValue_QueuesTheEdge_VisibleAfterApplyCommands()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        world.ApplyCommands();

        world[a].AddRelation(b, new Likes { Weight = 4f });
        world.ApplyCommands();

        world.TryGetRelation<Likes>(a, b, out _).Weight.Should().Be(4f);
    }

    [Fact]
    public void AddRelation_MarkerOnly_QueuesTheEdge_VisibleAfterApplyCommands()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        world.ApplyCommands();

        world[a].AddRelation<Follows>(b);
        world.ApplyCommands();

        world.HasRelation<Follows>(a, b).Should().BeTrue();
    }

    [Fact]
    public void RemoveRelation_QueuesTheRemove_VisibleAfterApplyCommands()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.ApplyCommands();

        world[a].RemoveRelation<Likes>(b);
        world.ApplyCommands();

        world.HasRelation<Likes>(a, b).Should().BeFalse();
    }

    [Fact]
    public void Targets_ReturnsEveryEdgeAndItsPayload()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        Entity c = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.Commands.AddRelation(a, c, new Likes { Weight = 2f });
        world.ApplyCommands();

        world[a].Targets<Likes>().Should().HaveCount(2);
    }

    [Fact]
    public void Sources_ReturnsEverySourcePointingAtThisEntity()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        Entity target = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, target, new Likes { Weight = 1f });
        world.Commands.AddRelation(b, target, new Likes { Weight = 2f });
        world.ApplyCommands();

        world[target].Sources<Likes>().Should().BeEquivalentTo([a, b]);
    }

    [Fact]
    public void IsAlive_LiveEntity_ReturnsTrue()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        world[entity].IsAlive.Should().BeTrue();
    }

    [Fact]
    public void IsAlive_AfterDestroyEntity_ReturnsFalse()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        world[entity].DestroyEntity();
        world.ApplyCommands();

        world[entity].IsAlive.Should().BeFalse();
    }

    [Fact]
    public void PermanentId_MatchesWorldGetPermanentId()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        world[entity].PermanentId.Should().Be(world.GetPermanentId(entity));
    }

    [Fact]
    public void DestroyEntity_QueuesTheDestroy_VisibleAfterApplyCommands()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        world[entity].DestroyEntity();
        world.IsAlive(entity).Should().BeTrue(); // still deferred
        world.ApplyCommands();

        world.IsAlive(entity).Should().BeFalse();
    }

    [Fact]
    public void Chaining_MultipleMutationsAcrossComponentsAndTags_AllLandAfterApplyCommands()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        world[entity].AddComponent(new Position { X = 1f }).AddTag<Flag>();
        world.ApplyCommands();

        world.GetComponent<Position>(entity).X.Should().Be(1f);
        world.HasTag<Flag>(entity).Should().BeTrue();
    }

    [Fact]
    public void CreateEntity_Bare_ReturnsAnEntityViewThatCanBeChainedImmediately()
    {
        var world = new World();

        var view = world.Commands.CreateEntity().AddComponent(new Position { X = 4f });
        world.ApplyCommands();

        world.GetComponent<Position>(view.Entity).X.Should().Be(4f);
    }

    [Fact]
    public void CreateEntity_WithInitialComponent_ReturnsAnEntityViewThatCanBeChainedImmediately()
    {
        var world = new World();

        var view = world.Commands.CreateEntity(new Position { X = 1f }).AddTag<Flag>();
        world.ApplyCommands();

        world.HasTag<Flag>(view.Entity).Should().BeTrue();
    }
}
