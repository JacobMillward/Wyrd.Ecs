namespace Wyrd.Ecs.Tests;

struct C0 : IComponent { public int Value; }
struct C1 : IComponent { public int Value; }
struct C2 : IComponent { public int Value; }
struct C3 : IComponent { public int Value; }
struct C4 : IComponent { public int Value; }
struct C5 : IComponent { public int Value; }
struct C6 : IComponent { public int Value; }
struct C7 : IComponent { public int Value; }
struct C8 : IComponent { public int Value; }
struct C9 : IComponent { public int Value; }
struct C10 : IComponent { public int Value; }
struct C11 : IComponent { public int Value; }

public class QueryArityBoundaryTests
{
    [Fact]
    public void TwelveComponentShape_CompilesAndExecutesCorrectly_WellPastTheOldArityCapOfEight()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity();
        world.Commands.AddComponent(entity, new C0 { Value = 0 });
        world.Commands.AddComponent(entity, new C1 { Value = 1 });
        world.Commands.AddComponent(entity, new C2 { Value = 2 });
        world.Commands.AddComponent(entity, new C3 { Value = 3 });
        world.Commands.AddComponent(entity, new C4 { Value = 4 });
        world.Commands.AddComponent(entity, new C5 { Value = 5 });
        world.Commands.AddComponent(entity, new C6 { Value = 6 });
        world.Commands.AddComponent(entity, new C7 { Value = 7 });
        world.Commands.AddComponent(entity, new C8 { Value = 8 });
        world.Commands.AddComponent(entity, new C9 { Value = 9 });
        world.Commands.AddComponent(entity, new C10 { Value = 10 });
        world.Commands.AddComponent(entity, new C11 { Value = 11 });
        world.ApplyCommands();

        var sum = 0;
        var found = false;
        world.Query()
            .With<C0>().With<C1>().With<C2>().With<C3>()
            .With<C4>().With<C5>().With<C6>().With<C7>()
            .With<C8>().With<C9>().With<C10>().With<C11>()
            .ForEach(0, (in int _, ref C0 c0, ref C1 c1, ref C2 c2, ref C3 c3,
                ref C4 c4, ref C5 c5, ref C6 c6, ref C7 c7, ref C8 c8, ref C9 c9, ref C10 c10, ref C11 c11) =>
            {
                sum = c0.Value + c1.Value + c2.Value + c3.Value + c4.Value + c5.Value
                    + c6.Value + c7.Value + c8.Value + c9.Value + c10.Value + c11.Value;
                found = true;
            });

        found.Should().BeTrue();
        sum.Should().Be(0 + 1 + 2 + 3 + 4 + 5 + 6 + 7 + 8 + 9 + 10 + 11);
    }

    [Fact]
    public void TwelveComponentShape_MissingOneComponent_DoesNotMatch()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity();
        world.Commands.AddComponent(entity, new C0());
        world.Commands.AddComponent(entity, new C1());
        world.Commands.AddComponent(entity, new C2());
        world.Commands.AddComponent(entity, new C3());
        world.Commands.AddComponent(entity, new C4());
        world.Commands.AddComponent(entity, new C5());
        world.Commands.AddComponent(entity, new C6());
        world.Commands.AddComponent(entity, new C7());
        world.Commands.AddComponent(entity, new C8());
        world.Commands.AddComponent(entity, new C9());
        world.Commands.AddComponent(entity, new C10()); // no C11
        world.ApplyCommands();

        var found = false;
        world.Query()
            .With<C0>().With<C1>().With<C2>().With<C3>()
            .With<C4>().With<C5>().With<C6>().With<C7>()
            .With<C8>().With<C9>().With<C10>().With<C11>()
            .ForEach(0, (in int _, ref C0 c0, ref C1 c1, ref C2 c2, ref C3 c3,
                ref C4 c4, ref C5 c5, ref C6 c6, ref C7 c7, ref C8 c8, ref C9 c9, ref C10 c10, ref C11 c11) => found = true);

        found.Should().BeFalse();
    }
}
