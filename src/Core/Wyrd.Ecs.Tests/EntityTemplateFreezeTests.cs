namespace Wyrd.Ecs.Tests;

public class EntityTemplateFreezeTests
{
    private struct Position : IComponent { public float X; }
    private struct Flag : ITag;

    [Fact]
    public void AddComponent_AfterInstantiation_Throws()
    {
        var world = new World();
        var template = new EntityTemplate().AddComponent(new Position { X = 1f });
        world.Commands.CreateEntity(template);
        world.ApplyCommands();

        var act = () => template.AddComponent(new Position { X = 2f });

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddTag_AfterInstantiation_Throws()
    {
        var world = new World();
        var template = new EntityTemplate().AddComponent(new Position { X = 1f });
        world.Commands.CreateEntity(template);
        world.ApplyCommands();

        var act = () => template.AddTag<Flag>();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddChild_AfterInstantiation_Throws()
    {
        var world = new World();
        var template = new EntityTemplate().AddComponent(new Position { X = 1f });
        world.Commands.CreateEntity(template);
        world.ApplyCommands();

        var act = () => template.AddChild(new EntityTemplate());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddParent_AfterInstantiation_Throws()
    {
        var world = new World();
        var template = new EntityTemplate().AddComponent(new Position { X = 1f });
        Entity parent = world.Commands.CreateEntity();
        world.Commands.CreateEntity(template);
        world.ApplyCommands();

        var act = () => template.AddParent(parent);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddComponent_BeforeInstantiation_StillWorks()
    {
        var template = new EntityTemplate();

        var act = () => template.AddComponent(new Position { X = 1f });

        act.Should().NotThrow();
    }
}
