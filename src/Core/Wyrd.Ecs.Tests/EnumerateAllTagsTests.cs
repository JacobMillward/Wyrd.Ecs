namespace Wyrd.Ecs.Tests;

public class EnumerateAllTagsTests
{
    private struct Position : IComponent { }
    private struct Enemy : ITag { }

    [Fact]
    public void EnumerateAllTags_YieldsOneEntryPerTaggedEntity()
    {
        var world = new WorldBuilder().Build();
        var entity = world.Commands.CreateEntity(new Position());
        world.Commands.AddTag<Enemy>(entity);
        world.ApplyCommands();

        var registry = new CodecRegistry();
        registry.RegisterTag<Enemy>("Enemy");

        var tags = world.EnumerateAllTags(registry);

        tags.Should().ContainSingle(t => t.Discriminator == "Enemy");
    }

    [Fact]
    public void EnumerateAllTags_UnregisteredTag_IsSkipped()
    {
        var world = new WorldBuilder().Build();
        var entity = world.Commands.CreateEntity();
        world.Commands.AddTag<Enemy>(entity);
        world.ApplyCommands();

        var tags = world.EnumerateAllTags(new CodecRegistry());

        tags.Should().BeEmpty();
    }

    [Fact]
    public void EnumerateAllTags_OnAnEmptyWorld_YieldsNothing()
    {
        var world = new WorldBuilder().Build();

        var registry = new CodecRegistry();
        registry.RegisterTag<Enemy>("Enemy");

        world.EnumerateAllTags(registry).Should().BeEmpty();
    }
}
