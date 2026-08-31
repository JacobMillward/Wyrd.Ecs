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
    public void OneMatch_ReturnsTrueWithTheRow()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity(new TrySinglePosition { X = 1f });
        world.ApplyCommands();

        var found = world.Query().With<TrySinglePosition>().TrySingle(out var row);

        found.Should().BeTrue();
        row.Entity.Should().Be(entity);
        row.TrySinglePosition.X.Should().Be(1f);
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
    public void WithMut_RowFieldIsLiveAndMutable()
    {
        var world = new World();
        world.Commands.CreateEntity(new TrySinglePosition { X = 1f });
        world.ApplyCommands();

        world.Query().WithMut<TrySinglePosition>().TrySingle(out var row);
        row.TrySinglePosition.X += 1f;

        var total = 0f;
        world.Query().With<TrySinglePosition>().ForEach((in TrySinglePosition p) => total += p.X);
        total.Should().Be(2f);
    }

    [Fact]
    public void Row_DestroyEntity_QueuesDestruction()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity(new TrySinglePosition());
        world.ApplyCommands();

        world.Query().With<TrySinglePosition>().TrySingle(out var row);
        row.DestroyEntity();
        world.ApplyCommands();

        world[entity].IsAlive.Should().BeFalse();
    }
}
