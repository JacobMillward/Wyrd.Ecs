namespace Wyrd.Ecs.Tests;

public class RelationDestroyCascadeTests
{
    private struct Likes : IRelation
    {
        public float Weight;
    }

    private struct Follows : IRelation;

    [Fact]
    public void DestroyingTheSource_RemovesTheEdgeFromTheTargetsBacklinks()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.ApplyCommands();

        world.Commands.DestroyEntity(a);
        world.ApplyCommands();

        world.HasComponent<RelationBacklinks<Likes>>(b).Should().BeFalse();
    }

    [Fact]
    public void DestroyingTheTarget_RemovesTheEdgeFromTheSourcesLinks()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.ApplyCommands();

        world.Commands.DestroyEntity(b);
        world.ApplyCommands();

        world.HasComponent<RelationLinks<Likes>>(a).Should().BeFalse(); // b was a's only edge -- now empty, so the component itself is removed
        world.Targets<Likes>(a).Should().BeEmpty();
    }

    [Fact]
    public void DestroyingTheSource_WithManyTargets_CleansEveryOne()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        var c = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.Commands.AddRelation(a, c, new Likes { Weight = 2f });
        world.ApplyCommands();

        world.Commands.DestroyEntity(a);
        world.ApplyCommands();

        world.HasComponent<RelationBacklinks<Likes>>(b).Should().BeFalse();
        world.HasComponent<RelationBacklinks<Likes>>(c).Should().BeFalse();
    }

    [Fact]
    public void DestroyingTheTarget_WithManySources_CleansEveryOne()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        var target = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, target, new Likes { Weight = 1f });
        world.Commands.AddRelation(b, target, new Likes { Weight = 2f });
        world.ApplyCommands();

        world.Commands.DestroyEntity(target);
        world.ApplyCommands();

        world.Targets<Likes>(a).Should().BeEmpty();
        world.Targets<Likes>(b).Should().BeEmpty();
    }

    [Fact]
    public void DestroyingASelfRelatedEntity_CleansUpWithoutCorruptingItsOtherComponents()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, a, new Likes { Weight = 1f }); // both RelationLinks<Likes> and RelationBacklinks<Likes> land on `a`
        world.ApplyCommands();

        var act = () => { world.Commands.DestroyEntity(a); world.ApplyCommands(); };

        act.Should().NotThrow();
        world.IsAlive(a).Should().BeFalse();
    }

    [Fact]
    public void DestroyingAnUnrelatedEntity_DoesNotTouchSomeoneElsesEdges()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        var bystander = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.ApplyCommands();

        world.Commands.DestroyEntity(bystander);
        world.ApplyCommands();

        world.HasRelation<Likes>(a, b).Should().BeTrue();
    }

    [Fact]
    public void DestroyingTheSource_OfAMarkerOnlyRelation_RemovesTheBacklink()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        world.Commands.AddRelation<Follows>(a, b);
        world.ApplyCommands();

        world.Commands.DestroyEntity(a);
        world.ApplyCommands();

        world.HasComponent<RelationBacklinks<Follows>>(b).Should().BeFalse();
    }

    [Fact]
    public void DestroyingTheTarget_OfAMarkerOnlyRelation_RemovesTheLink()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        world.Commands.AddRelation<Follows>(a, b);
        world.ApplyCommands();

        world.Commands.DestroyEntity(b);
        world.ApplyCommands();

        world.Targets<Follows>(a).Should().BeEmpty();
    }

    [Fact]
    public void DestroyingTheTarget_OfANonDependentRelation_OnlyUnlinks_DoesNotDestroySources()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.ApplyCommands();

        world.Commands.DestroyEntity(b);
        world.ApplyCommands();

        world.IsAlive(a).Should().BeTrue(); // Likes is not IDependent -- a survives, just unlinked
        world.HasRelation<Likes>(a, b).Should().BeFalse();
    }
}
