using Likes = Wyrd.Ecs.Tests.RelationQueryChainSugarLikes;

namespace Wyrd.Ecs.Tests;

// File-scoped, not nested private: the query-chain generator emits code in a separate
// partial that can't see a private nested type. Matches QueryFluentBuilderTests.cs's
// existing convention for any component type used with .With<T>().ForEach(...).
struct RelationQueryChainSugarLikes : IRelation { public float Weight; }
struct RelationQueryChainSugarPosition : IComponent { public float X; }

public class RelationQueryChainSugarTests
{
    [Fact]
    public void WithRelation_MatchesEntitiesWithAtLeastOneEdge_RegardlessOfTarget()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        Entity c = world.Commands.CreateEntity();
        Entity untouched = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, c, new Likes { Weight = 1f });
        world.Commands.AddRelation(b, c, new Likes { Weight = 2f });
        world.ApplyCommands();

        var count = 0;
        world.Query().WithRelation<Likes>()
            .ForEach(0, (in int _, in RelationLinks<Likes> link) => count++);

        count.Should().Be(2); // a and b, both matched purely by "has any Likes edge": untouched and c (backlinks only) are excluded
    }

    [Fact]
    public void WithoutRelation_ExcludesEntitiesWithAnEdge()
    {
        var world = new World();
        Entity withEdge = world.Commands.CreateEntity();
        Entity withoutEdge = world.Commands.CreateEntity();
        Entity target = world.Commands.CreateEntity();
        world.Commands.AddComponent(withEdge, new RelationQueryChainSugarPosition { X = 1f });
        world.Commands.AddComponent(withoutEdge, new RelationQueryChainSugarPosition { X = 2f });
        world.Commands.AddRelation(withEdge, target, new Likes { Weight = 1f });
        world.ApplyCommands();

        var matched = new List<float>();
        world.Query().With<RelationQueryChainSugarPosition>().WithoutRelation<Likes>()
            .ForEach(matched, (in List<float> m, in RelationQueryChainSugarPosition p) => m.Add(p.X));

        matched.Should().Equal(2f);
    }
}
