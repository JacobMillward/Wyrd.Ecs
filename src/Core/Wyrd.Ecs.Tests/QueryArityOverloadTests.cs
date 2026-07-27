namespace Wyrd.Ecs.Tests;

struct ArityPosition : IComponent { public float X; }
struct ArityVelocity : IComponent { public float X; }
struct ArityFrozen : ITag;

public class QueryArityOverloadTests
{
    [Fact]
    public void With_TwoArgs_ProducesTheSameShapeAsChainingIndividually()
    {
        var world = new World();

        var chained = world.Query().With<ArityPosition>().With<ArityVelocity>();
        var collapsed = world.Query().With<ArityPosition, ArityVelocity>();

        chained.GetType().Should().Be(collapsed.GetType());
    }

    [Fact]
    public void Without_TwoArgs_ProducesTheSameShapeAsChainingIndividually()
    {
        var world = new World();

        var chained = world.Query().Without<ArityPosition>().Without<ArityVelocity>();
        var collapsed = world.Query().Without<ArityPosition, ArityVelocity>();

        chained.GetType().Should().Be(collapsed.GetType());
    }

    [Fact]
    public void Has_TwoArgs_ProducesTheSameShapeAsChainingIndividually()
    {
        var world = new World();

        var chained = world.Query().Has<ArityPosition>().Has<ArityVelocity>();
        var collapsed = world.Query().Has<ArityPosition, ArityVelocity>();

        chained.GetType().Should().Be(collapsed.GetType());
    }

    [Fact]
    public void CollapsedWith_StillChainsAfterward()
    {
        var world = new World();

        // Compiles iff Query<TShape>.With<T0,T1>()'s return type still exposes .Without<T>()
        // -- proves arity overloads don't special-case away ordinary chaining.
        var query = world.Query().With<ArityPosition, ArityVelocity>().Without<ArityFrozen>();

        query.Should().NotBeNull();
    }

    [Fact]
    public void CollapsedWith_MatchesArchetypesWithBothComponents()
    {
        var world = new World();
        world.Commands.CreateEntity(new ArityPosition { X = 1f }, new ArityVelocity { X = 2f });
        world.Commands.CreateEntity(new ArityPosition { X = 10f }); // ArityPosition only -- shouldn't match
        world.ApplyCommands();

        var total = 0f;
        world.Query().With<ArityPosition, ArityVelocity>()
            .ForEach(0, (in int _, ref ArityPosition p, in ArityVelocity v) => total += p.X + v.X);

        total.Should().Be(3f);
    }
}
