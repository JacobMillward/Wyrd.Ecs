namespace Wyrd.Ecs.Tests;

file sealed class OrderTestSystemA : EcsSystem
{
    protected override void Execute(World world, Time time) { }
}

file sealed class OrderTestSystemB : EcsSystem
{
    protected override void Execute(World world, Time time) { }
}

file sealed class OrderTestMarker : MarkerSystem
{
}

public class OrderedSystemTests
{
    [Fact]
    public void PlainEcsSystem_ImplicitlyConvertsToOrderedSystemWithNoEdges()
    {
        var system = new OrderTestSystemA();

        OrderedSystem ordered = system;

        ordered.System.Should().BeSameAs(system);
        ordered.BeforeTargets.Should().BeEmpty();
        ordered.AfterTargets.Should().BeEmpty();
    }

    [Fact]
    public void Before_AddsTheTargetTypeToBeforeTargets()
    {
        var ordered = Order.For(new OrderTestSystemA()).Before<OrderTestSystemB>();

        ordered.BeforeTargets.Should().Equal(typeof(OrderTestSystemB));
        ordered.AfterTargets.Should().BeEmpty();
    }

    [Fact]
    public void After_AddsTheTargetTypeToAfterTargets()
    {
        var ordered = Order.For(new OrderTestSystemA()).After<OrderTestMarker>();

        ordered.AfterTargets.Should().Equal(typeof(OrderTestMarker));
        ordered.BeforeTargets.Should().BeEmpty();
    }

    [Fact]
    public void ChainedBeforeAndAfter_AccumulateBothFlat()
    {
        var ordered = Order.For(new OrderTestSystemA())
            .After<OrderTestMarker>()
            .Before<OrderTestSystemB>();

        ordered.AfterTargets.Should().Equal(typeof(OrderTestMarker));
        ordered.BeforeTargets.Should().Equal(typeof(OrderTestSystemB));
    }

    [Fact]
    public void RunBeforeAttribute_ExposesItsTargetType()
    {
        var attribute = new RunBeforeAttribute(typeof(OrderTestSystemB));

        attribute.Target.Should().Be(typeof(OrderTestSystemB));
    }

    [Fact]
    public void RunAfterAttribute_ExposesItsTargetType()
    {
        var attribute = new RunAfterAttribute(typeof(OrderTestMarker));

        attribute.Target.Should().Be(typeof(OrderTestMarker));
    }
}
