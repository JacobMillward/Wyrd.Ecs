namespace Wyrd.Ecs.Tests;

// Named uniquely to avoid colliding with QueryFluentBuilderTests.cs's types in this same
// namespace, and not `file`-scoped: the source generator's `.ForEach` extensions live in a
// separate generated file that can't see a `file`-scoped type from here.
struct ChainPosition : IComponent { public float X; }
struct ChainVelocity : IComponent { public float X; }
struct ChainDead : ITag;
struct ChainBuffA : ITag;
struct ChainBuffB : ITag;

public class QueryChainTests
{
    [Fact]
    public void Query_ReturnsTheEntryPointQuery()
    {
        var world = new World();

        var chain = world.Query();

        chain.Should().BeOfType<Query>();
    }

    [Fact]
    public void With_PrependsTheComponentOntoTheShape()
    {
        var world = new World();

        var chain = world.Query().With<ChainPosition>();

        chain.Should().BeOfType<Query<(ChainPosition, Nil)>>();
    }

    [Fact]
    public void ChainedWith_NestsInCallOrder()
    {
        var world = new World();

        var chain = world.Query().With<ChainPosition>().With<ChainVelocity>();

        chain.Should().BeOfType<Query<(ChainVelocity, (ChainPosition, Nil))>>();
    }

    [Fact]
    public void WithMut_WrapsTheComponentInWriteMarker_ADifferentTypeThanWith()
    {
        var world = new World();

        var chain = world.Query().WithMut<ChainPosition>();

        // Deliberately NOT the same type as .With<ChainPosition>() -- see WriteMarker<T>'s own
        // doc comment for why: two colliding TrySingle()/foreach-consumed-query call sites for
        // the same component, one read-only and one mutable, need genuinely different closed
        // Query<TShape> types to stay unambiguous, not the same type reached two ways.
        chain.Should().BeOfType<Query<(WriteMarker<ChainPosition>, Nil)>>();
    }

    [Fact]
    public void ChainedWithAndWithMut_NestInCallOrderTogether()
    {
        var world = new World();

        var chain = world.Query().With<ChainPosition>().WithMut<ChainVelocity>();

        chain.Should().BeOfType<Query<(WriteMarker<ChainVelocity>, (ChainPosition, Nil))>>();
    }

    [Fact]
    public void WithMut_MultiArity_MatchesChainedSingleArityShape()
    {
        var world = new World();

        var multiArity = world.Query().WithMut<ChainPosition, ChainVelocity>();
        var chained = world.Query().WithMut<ChainPosition>().WithMut<ChainVelocity>();

        multiArity.Should().BeOfType(chained.GetType());
    }

    [Fact]
    public void Without_DoesNotChangeTheShape_OnlyTheFilter()
    {
        var world = new World();

        var chain = world.Query().With<ChainPosition>().Without<ChainDead>();

        chain.Should().BeOfType<Query<(ChainPosition, Nil)>>();
        chain.Filter.Should().Be(ArchetypeQuery.Empty.Without<ChainDead>());
    }

    [Fact]
    public void Any_DoesNotChangeTheShape_OnlyTheFilter()
    {
        var world = new World();

        var chain = world.Query().With<ChainPosition>().Any<ChainBuffA, ChainBuffB>();

        chain.Should().BeOfType<Query<(ChainPosition, Nil)>>();
        chain.Filter.Should().Be(ArchetypeQuery.Empty.Any<ChainBuffA, ChainBuffB>());
    }

    [Fact]
    public void FilterCallsDoNotChangeTheShapeType_SoTheyCanBeAppliedConditionally()
    {
        var world = new World();

        var q = world.Query().With<ChainPosition>();
        if (true) q = q.Without<ChainDead>(); // must compile: same Query<TShape> before and after

        q.Should().BeOfType<Query<(ChainPosition, Nil)>>();
    }

    [Fact]
    public void FilterAppliedBeforeALaterWith_SurvivesIntoTheNewShape()
    {
        var world = new World();
        Entity alive = world.Commands.CreateEntity();
        world.Commands.AddComponent(alive, new ChainPosition { X = 1f });
        world.Commands.AddComponent(alive, new ChainVelocity { X = 2f });
        Entity dead = world.Commands.CreateEntity();
        world.Commands.AddComponent(dead, new ChainPosition { X = 3f });
        world.Commands.AddComponent(dead, new ChainVelocity { X = 4f });
        world.Commands.AddTag<ChainDead>(dead);
        world.ApplyCommands();

        var matched = new List<float>();
        world.Query().With<ChainPosition>().Without<ChainDead>().With<ChainVelocity>()
            .ForEach(matched, (in List<float> m, ref ChainPosition p, in ChainVelocity v) => m.Add(p.X));

        matched.Should().Equal(1f);
    }
}
