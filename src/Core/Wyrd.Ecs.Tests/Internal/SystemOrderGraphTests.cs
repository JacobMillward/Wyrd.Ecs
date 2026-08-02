namespace Wyrd.Ecs.Tests.Internal;

using Wyrd.Ecs.Internal;

file sealed class GraphSystemA : EcsSystem { protected override void Execute(World world, Time time) { } }

file sealed class GraphSystemB : EcsSystem { protected override void Execute(World world, Time time) { } }

file sealed class GraphSystemC : EcsSystem { protected override void Execute(World world, Time time) { } }

file sealed class GraphMarker : MarkerSystem { }

file sealed class NoEdgeSystem : EcsSystem { protected override void Execute(World world, Time time) { } }

file class BaseWithEdge : EcsSystem { protected override void Execute(World world, Time time) { } }

file sealed class DerivedWithNoOwnEdge : BaseWithEdge { }

file sealed class NotASystem { }

file sealed class BadAttributeSystem : EcsSystem { protected override void Execute(World world, Time time) { } }

public class SystemOrderGraphTests
{
    /// <summary>
    /// Empty by default: most tests here exercise fluent <see cref="OrderedSystem"/>
    /// edges, which never consult this dictionary. Tests that exercise
    /// generator-emitted <c>[RunBefore]</c>/<c>[RunAfter]</c> discovery build their own,
    /// since <see cref="Internal.SystemOrderGraph.Resolve"/> no longer reads these
    /// attributes via reflection (moved to compile time, see
    /// <c>QueryChainGenerator.ExtractEdges</c>) — the fixture classes above are
    /// <c>file</c>-scoped, so the real generator would skip them entirely (a
    /// <c>file</c>-scoped type can never be referenced from the separate generated file
    /// <c>SystemRegistry.Edges</c> lives in), same as it does for <c>file</c>-scoped
    /// query components.
    /// </summary>
    private static readonly IReadOnlyDictionary<Type, (IReadOnlyList<Type> Before, IReadOnlyList<Type> After)> NoGeneratedEdges =
        new Dictionary<Type, (IReadOnlyList<Type>, IReadOnlyList<Type>)>();

    [Fact]
    public void RunBeforeAttribute_ProducesAnEdgeToTheTarget()
    {
        var a = new GraphSystemA();
        var b = new GraphSystemB();
        OrderedSystem[] systems = [a, b];
        var generatedEdges = new Dictionary<Type, (IReadOnlyList<Type>, IReadOnlyList<Type>)>
        {
            [typeof(GraphSystemB)] = ([typeof(GraphSystemA)], []),
        };

        var result = SystemOrderGraph.Resolve(systems, generatedEdges);

        result.Edges.Should().ContainSingle(e => e.Before == OrderNode.ForSystem(b) && e.After == OrderNode.ForSystem(a));
    }

    [Fact]
    public void RunAfterAttribute_ProducesAnEdgeFromTheTarget()
    {
        var a = new GraphSystemA();
        var c = new GraphSystemC();
        OrderedSystem[] systems = [a, c];
        var generatedEdges = new Dictionary<Type, (IReadOnlyList<Type>, IReadOnlyList<Type>)>
        {
            [typeof(GraphSystemC)] = ([], [typeof(GraphSystemA)]),
        };

        var result = SystemOrderGraph.Resolve(systems, generatedEdges);

        result.Edges.Should().ContainSingle(e => e.Before == OrderNode.ForSystem(a) && e.After == OrderNode.ForSystem(c));
    }

    [Fact]
    public void FluentAfter_ProducesAnEdgeFromTheTarget()
    {
        var a = new GraphSystemA();
        var noEdge = new NoEdgeSystem();
        OrderedSystem[] systems = [a, Order.For(noEdge).After<GraphSystemA>()];

        var result = SystemOrderGraph.Resolve(systems, NoGeneratedEdges);

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

        var result = SystemOrderGraph.Resolve(systems, NoGeneratedEdges);

        var markerNode = OrderNode.ForMarker(typeof(GraphMarker));
        result.Nodes.Should().ContainSingle(n => n == markerNode);
        result.Edges.Should().Contain(e => e.Before == markerNode && e.After == OrderNode.ForSystem(noEdge1));
        result.Edges.Should().Contain(e => e.Before == markerNode && e.After == OrderNode.ForSystem(noEdge2));
    }

    [Fact]
    public void EdgeTargetingAnUnregisteredType_Throws()
    {
        OrderedSystem[] systems = [Order.For(new NoEdgeSystem()).After<GraphSystemA>()]; // GraphSystemA never registered

        var act = () => SystemOrderGraph.Resolve(systems, NoGeneratedEdges);

        act.Should().Throw<InvalidOperationException>().WithMessage("*GraphSystemA*");
    }

    [Fact]
    public void EdgeTargetingATypeRegisteredTwice_Throws()
    {
        var duplicate1 = new GraphSystemA();
        var duplicate2 = new GraphSystemA();
        var dependent = Order.For(new NoEdgeSystem()).After<GraphSystemA>();
        OrderedSystem[] systems = [duplicate1, duplicate2, dependent];

        var act = () => SystemOrderGraph.Resolve(systems, NoGeneratedEdges);

        act.Should().Throw<InvalidOperationException>().WithMessage("*ambiguous*");
    }

    [Fact]
    public void AttributeTargetingATypeThatIsNeitherEcsSystemNorMarkerSystem_Throws()
    {
        OrderedSystem[] systems = [new BadAttributeSystem()];
        var generatedEdges = new Dictionary<Type, (IReadOnlyList<Type>, IReadOnlyList<Type>)>
        {
            [typeof(BadAttributeSystem)] = ([typeof(NotASystem)], []),
        };

        var act = () => SystemOrderGraph.Resolve(systems, generatedEdges);

        act.Should().Throw<InvalidOperationException>().WithMessage("*NotASystem*");
    }

    [Fact]
    public void RunBeforeAttribute_IsNotInheritedByASubclassWithNoOwnAttribute()
    {
        var a = new GraphSystemA();
        var derived = new DerivedWithNoOwnEdge();
        OrderedSystem[] systems = [a, derived];
        // BaseWithEdge (not DerivedWithNoOwnEdge) is the declaring type an edge would be
        // keyed under — present here to prove the lookup is by derived's own exact Type
        // (DerivedWithNoOwnEdge), which never matches this entry, not because no entry
        // for the hierarchy exists at all.
        var generatedEdges = new Dictionary<Type, (IReadOnlyList<Type>, IReadOnlyList<Type>)>
        {
            [typeof(BaseWithEdge)] = ([typeof(GraphSystemA)], []),
        };

        var result = SystemOrderGraph.Resolve(systems, generatedEdges);

        result.Edges.Should().NotContain(e => e.After == OrderNode.ForSystem(a));
    }
}
