namespace Wyrd.Ecs.Tests.Internal;

using Wyrd.Ecs.Internal;

file sealed class GraphSystemA : EcsSystem { protected override void Execute(World world, Time time) { } }

[RunBefore(typeof(GraphSystemA))]
file sealed class GraphSystemB : EcsSystem { protected override void Execute(World world, Time time) { } }

[RunAfter(typeof(GraphSystemA))]
file sealed class GraphSystemC : EcsSystem { protected override void Execute(World world, Time time) { } }

file sealed class GraphMarker : MarkerSystem { }

file sealed class NoEdgeSystem : EcsSystem { protected override void Execute(World world, Time time) { } }

[RunBefore(typeof(GraphSystemA))]
file class BaseWithEdge : EcsSystem { protected override void Execute(World world, Time time) { } }

file sealed class DerivedWithNoOwnEdge : BaseWithEdge { }

file sealed class NotASystem { }

[RunBefore(typeof(NotASystem))]
file sealed class BadAttributeSystem : EcsSystem { protected override void Execute(World world, Time time) { } }

public class SystemOrderGraphTests
{
    [Fact]
    public void RunBeforeAttribute_ProducesAnEdgeToTheTarget()
    {
        var a = new GraphSystemA();
        var b = new GraphSystemB();
        OrderedSystem[] systems = [a, b];

        var result = SystemOrderGraph.Resolve(systems);

        result.Edges.Should().ContainSingle(e => e.Before == OrderNode.ForSystem(b) && e.After == OrderNode.ForSystem(a));
    }

    [Fact]
    public void RunAfterAttribute_ProducesAnEdgeFromTheTarget()
    {
        var a = new GraphSystemA();
        var c = new GraphSystemC();
        OrderedSystem[] systems = [a, c];

        var result = SystemOrderGraph.Resolve(systems);

        result.Edges.Should().ContainSingle(e => e.Before == OrderNode.ForSystem(a) && e.After == OrderNode.ForSystem(c));
    }

    [Fact]
    public void FluentAfter_ProducesAnEdgeFromTheTarget()
    {
        var a = new GraphSystemA();
        var noEdge = new NoEdgeSystem();
        OrderedSystem[] systems = [a, Order.For(noEdge).After<GraphSystemA>()];

        var result = SystemOrderGraph.Resolve(systems);

        result.Edges.Should().ContainSingle(e => e.Before == OrderNode.ForSystem(a) && e.After == OrderNode.ForSystem(noEdge));
    }

    [Fact]
    public void MarkerTargetedByMultipleEdges_IsSynthesizedOnceAndShared()
    {
        var noEdge1 = new NoEdgeSystem();
        var noEdge2 = new NoEdgeSystem();
        OrderedSystem[] systems =
        [
            Order.For(noEdge1).After<GraphMarker>(),
            Order.For(noEdge2).After<GraphMarker>(),
        ];

        var result = SystemOrderGraph.Resolve(systems);

        var markerNode = OrderNode.ForMarker(typeof(GraphMarker));
        result.Nodes.Should().ContainSingle(n => n == markerNode);
        result.Edges.Should().Contain(e => e.Before == markerNode && e.After == OrderNode.ForSystem(noEdge1));
        result.Edges.Should().Contain(e => e.Before == markerNode && e.After == OrderNode.ForSystem(noEdge2));
    }

    [Fact]
    public void EdgeTargetingAnUnregisteredType_Throws()
    {
        OrderedSystem[] systems = [Order.For(new NoEdgeSystem()).After<GraphSystemA>()]; // GraphSystemA never registered

        var act = () => SystemOrderGraph.Resolve(systems);

        act.Should().Throw<InvalidOperationException>().WithMessage("*GraphSystemA*");
    }

    [Fact]
    public void EdgeTargetingATypeRegisteredTwice_Throws()
    {
        var duplicate1 = new GraphSystemA();
        var duplicate2 = new GraphSystemA();
        var dependent = Order.For(new NoEdgeSystem()).After<GraphSystemA>();
        OrderedSystem[] systems = [duplicate1, duplicate2, dependent];

        var act = () => SystemOrderGraph.Resolve(systems);

        act.Should().Throw<InvalidOperationException>().WithMessage("*ambiguous*");
    }

    [Fact]
    public void AttributeTargetingATypeThatIsNeitherEcsSystemNorMarkerSystem_Throws()
    {
        OrderedSystem[] systems = [new BadAttributeSystem()];

        var act = () => SystemOrderGraph.Resolve(systems);

        act.Should().Throw<InvalidOperationException>().WithMessage("*NotASystem*");
    }

    [Fact]
    public void RunBeforeAttribute_IsNotInheritedByASubclassWithNoOwnAttribute()
    {
        var a = new GraphSystemA();
        var derived = new DerivedWithNoOwnEdge();
        OrderedSystem[] systems = [a, derived];

        var result = SystemOrderGraph.Resolve(systems);

        result.Edges.Should().NotContain(e => e.After == OrderNode.ForSystem(a));
    }
}
