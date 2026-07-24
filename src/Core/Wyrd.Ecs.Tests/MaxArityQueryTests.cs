namespace Wyrd.Ecs.Tests;

public class MaxArityQueryTests
{
    internal struct C0 : IComponent { public int Value; }
    internal struct C1 : IComponent { public int Value; }
    internal struct C2 : IComponent { public int Value; }
    internal struct C3 : IComponent { public int Value; }
    internal struct C4 : IComponent { public int Value; }
    internal struct C5 : IComponent { public int Value; }
    internal struct C6 : IComponent { public int Value; }
    internal struct C7 : IComponent { public int Value; }

    [Fact]
    public void CreateEntity_EightComponents_AllPersistAndQueryTogether()
    {
        var world = new World();
        world.Commands.CreateEntity(
            new C0 { Value = 0 }, new C1 { Value = 1 }, new C2 { Value = 2 }, new C3 { Value = 3 },
            new C4 { Value = 4 }, new C5 { Value = 5 }, new C6 { Value = 6 }, new C7 { Value = 7 });
        world.ApplyCommands();

        var found = false;
        foreach (var row in world.Query<C0, C1, C2, C3, C4, C5, C6, C7>())
        {
            row.Get<C0>().Value.Should().Be(0);
            row.Get<C1>().Value.Should().Be(1);
            row.Get<C2>().Value.Should().Be(2);
            row.Get<C3>().Value.Should().Be(3);
            row.Get<C4>().Value.Should().Be(4);
            row.Get<C5>().Value.Should().Be(5);
            row.Get<C6>().Value.Should().Be(6);
            row.Get<C7>().Value.Should().Be(7);
            found = true;
        }
        found.Should().BeTrue();
    }
}
