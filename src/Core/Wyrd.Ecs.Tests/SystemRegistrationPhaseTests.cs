namespace Wyrd.Ecs.Tests;

file sealed class FluentPhaseProbeA : EcsSystem
{
    public static int TickCount;
    public static List<string>? Order;
    protected override void Execute(World world, Time time)
    {
        TickCount++;
        Order?.Add("A");
    }
}

file sealed class FluentPhaseProbeB : EcsSystem
{
    public static int TickCount;
    public static List<string>? Order;
    protected override void Execute(World world, Time time)
    {
        TickCount++;
        Order?.Add("B");
    }
}

public class SystemRegistrationPhaseTests
{
    [Fact]
    public void Phase_PreUpdate_RunsBeforeAnOrdinarySystem_ThroughARealWorldBuilder()
    {
        var order = new List<string>();
        FluentPhaseProbeA.Order = order;
        FluentPhaseProbeB.Order = order;
        try
        {
            var world = new WorldBuilder()
                .AddSystemCore(
                    typeof(FluentPhaseProbeB),
                    access: null,
                    construct: _ => new FluentPhaseProbeB(),
                    generatedBeforeTargets: [],
                    generatedAfterTargets: [])
                .Build();
            // Registered after B, but .Phase(Phase.PreUpdate) must still place A first -
            // proves the fluent call actually drives real scheduling order, not registration order.
            world.AddSystemCore(
                typeof(FluentPhaseProbeA),
                access: null,
                construct: _ => new FluentPhaseProbeA(),
                generatedBeforeTargets: [],
                generatedAfterTargets: [])
                .Phase(Phase.PreUpdate);

            world.Update(TimeSpan.Zero);

            order.Should().Equal("A", "B");
        }
        finally
        {
            FluentPhaseProbeA.Order = null;
            FluentPhaseProbeB.Order = null;
        }
    }

    [Fact]
    public void Phase_Update_IsANoOp_RegistrationSucceedsWithNoEdgesAdded()
    {
        var world = new WorldBuilder()
            .AddSystemCore(
                typeof(FluentPhaseProbeA),
                access: null,
                construct: _ => new FluentPhaseProbeA(),
                generatedBeforeTargets: [],
                generatedAfterTargets: [])
                .Phase(Phase.Update)
            .Build();

        var act = () => world.Update(TimeSpan.Zero);

        act.Should().NotThrow();
    }
}
