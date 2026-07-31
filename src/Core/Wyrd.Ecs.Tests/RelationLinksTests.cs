namespace Wyrd.Ecs.Tests;

public class RelationLinksTests
{
    private struct Likes : IComponent
    {
        public float Weight;
    }

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
}
