namespace Wyrd.Ecs.Tests;

public class RelationLinksTests
{
    private struct Likes : IRelation
    {
        public float Weight;
    }

    private struct Follows : IRelation;

    [Fact]
    public void Constructor_ExposesTheGivenDictionaryThroughValues()
    {
        var a = new Entity(1, 0);
        var b = new Entity(2, 0);
        var backing = new Dictionary<Entity, Likes> { [b] = new Likes { Weight = 1f } };

        var links = new RelationLinks<Likes>(backing);

        links.Values.Should().ContainKey(b);
        links.Values[b].Weight.Should().Be(1f);
    }

    [Fact]
    public void Values_ReflectsLiveMutationsThroughTargets()
    {
        var backing = new Dictionary<Entity, Likes>();
        var links = new RelationLinks<Likes>(backing);
        var target = new Entity(3, 0);

        links.Targets!.Add(target, new Likes { Weight = 2f });

        links.Values.Should().ContainKey(target);
    }

    [Fact]
    public void ImplementsIComponent()
    {
        typeof(RelationLinks<Likes>).Should().BeAssignableTo<IComponent>();
    }

    [Fact]
    public void MarkerOnlyRelationType_IsAValidTypeArgument()
    {
        // Follows has no fields, so no separate tag-relation storage type is needed for
        // this (see IRelation's own doc); the empty struct is just the dictionary's value.
        var backing = new Dictionary<Entity, Follows> { [new Entity(1, 0)] = new Follows() };

        var links = new RelationLinks<Follows>(backing);

        links.Values.Should().ContainKey(new Entity(1, 0));
    }

    [Fact]
    public void Backlinks_Constructor_ExposesTheGivenSetThroughValues()
    {
        var source = new Entity(4, 0);
        var backing = new HashSet<Entity> { source };

        var backlinks = new RelationBacklinks<Likes>(backing);

        backlinks.Values.Should().Contain(source);
    }

    [Fact]
    public void Backlinks_ImplementsIComponent()
    {
        typeof(RelationBacklinks<Likes>).Should().BeAssignableTo<IComponent>();
    }
}
