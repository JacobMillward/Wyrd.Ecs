namespace Wyrd.Ecs.Tests;

public class RelationTagLinksTests
{
    private struct Follows : ITag;

    [Fact]
    public void Constructor_ExposesTheGivenSetThroughValues()
    {
        var target = new Entity(1, 0);
        var backing = new HashSet<Entity> { target };

        var links = new RelationTagLinks<Follows>(backing);

        links.Values.Should().Contain(target);
    }

    [Fact]
    public void Values_ReflectsLiveMutationsThroughTargets()
    {
        var backing = new HashSet<Entity>();
        var links = new RelationTagLinks<Follows>(backing);
        var target = new Entity(2, 0);

        links.Targets!.Add(target);

        links.Values.Should().Contain(target);
    }

    [Fact]
    public void RelationTagLinksAndBacklinks_ImplementIComponent()
    {
        typeof(RelationTagLinks<Follows>).Should().BeAssignableTo<IComponent>();
        typeof(RelationTagBacklinks<Follows>).Should().BeAssignableTo<IComponent>();
    }

    [Fact]
    public void RelationTagBacklinks_Constructor_ExposesTheGivenSetThroughValues()
    {
        var source = new Entity(3, 0);
        var backing = new HashSet<Entity> { source };

        var backlinks = new RelationTagBacklinks<Follows>(backing);

        backlinks.Values.Should().Contain(source);
    }
}
