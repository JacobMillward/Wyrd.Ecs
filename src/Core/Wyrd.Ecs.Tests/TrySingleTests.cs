namespace Wyrd.Ecs.Tests;

struct TrySinglePosition : IComponent { public float X; }

public class TrySingleTests
{
    [Fact]
    public void NoMatch_ReturnsFalse()
    {
        var world = new World();

        var found = world.Query().With<TrySinglePosition>().TrySingle(out _);

        found.Should().BeFalse();
    }

    [Fact]
    public void OneMatch_ReturnsTrueWithTheComponent()
    {
        var world = new World();
        world.Commands.CreateEntity(new TrySinglePosition { X = 1f });
        world.ApplyCommands();

        var found = world.Query().With<TrySinglePosition>().TrySingle(out var position);

        found.Should().BeTrue();
        position.X.Should().Be(1f);
    }

    [Fact]
    public void MultipleMatches_Throws()
    {
        var world = new World();
        world.Commands.CreateEntity(new TrySinglePosition { X = 1f });
        world.Commands.CreateEntity(new TrySinglePosition { X = 2f });
        world.ApplyCommands();

        var act = () => world.Query().With<TrySinglePosition>().TrySingle(out _);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void WithEntityView_ReturnsTheMatchingEntity()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity(new TrySinglePosition { X = 1f });
        world.ApplyCommands();

        var found = world.Query().With<TrySinglePosition>().TrySingle(out EntityView view, out var position);

        found.Should().BeTrue();
        ((Entity)view).Should().Be(entity);
        position.X.Should().Be(1f);
    }
}
