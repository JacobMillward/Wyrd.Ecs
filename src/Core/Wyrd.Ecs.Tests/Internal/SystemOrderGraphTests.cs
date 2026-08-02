namespace Wyrd.Ecs.Tests.Internal;

using Wyrd.Ecs.Internal;

file sealed class GraphSystemA : EcsSystem { protected override void Execute(World world, Time time) { } }

file sealed class GraphSystemB : EcsSystem { protected override void Execute(World world, Time time) { } }

file sealed class GraphSystemC : EcsSystem { protected override void Execute(World world, Time time) { } }

file sealed class GraphMarker : MarkerSystem { }

file sealed class NoEdgeSystem : EcsSystem { protected override void Execute(World world, Time time) { } }

file sealed class NotASystem { }

file sealed class BadTargetSystem : EcsSystem { protected override void Execute(World world, Time time) { } }

public class SystemOrderGraphTests
{
    /// <summary>
    /// A resolved <see cref="SystemEntry"/> for an already-constructed instance, with
    /// whatever Before/After edges the test wants — <see cref="SystemOrderGraph.Resolve"/>
    /// now reads edges directly off each entry (already unioned from fluent
    /// <see cref="SystemRegistration"/> calls and generator-seeded
    /// <see cref="RunBeforeAttribute"/>/<see cref="RunAfterAttribute"/> declarations by
    /// the time it runs), not from a separate dictionary keyed by reflection.
    /// </summary>
    private static SystemEntry EntryFor(EcsSystem instance, IReadOnlyList<Type>? before = null, IReadOnlyList<Type>? after = null) =>
        new()
        {
            SystemType = instance.GetType(),
            Construct = _ => instance,
            Access = new SystemAccess([], []),
            Instance = instance,
            BeforeTargets = before is null ? [] : [.. before],
            AfterTargets = after is null ? [] : [.. after],
        };

    [Fact]
    public void BeforeEdge_ProducesAnEdgeToTheTarget()
    {
        var a = new GraphSystemA();
        var b = new GraphSystemB();
        SystemEntry[] entries = [EntryFor(a), EntryFor(b, before: [typeof(GraphSystemA)])];

        var result = SystemOrderGraph.Resolve(entries);

        result.Edges.Should().ContainSingle(e => e.Before == OrderNode.ForSystem(b) && e.After == OrderNode.ForSystem(a));
    }

    [Fact]
    public void AfterEdge_ProducesAnEdgeFromTheTarget()
    {
        var a = new GraphSystemA();
        var c = new GraphSystemC();
        SystemEntry[] entries = [EntryFor(a), EntryFor(c, after: [typeof(GraphSystemA)])];

        var result = SystemOrderGraph.Resolve(entries);

        result.Edges.Should().ContainSingle(e => e.Before == OrderNode.ForSystem(a) && e.After == OrderNode.ForSystem(c));
    }

    [Fact]
    public void MarkerTargetedByMultipleEdges_IsSynthesizedOnceAndShared()
    {
        var noEdge1 = new NoEdgeSystem();
        var noEdge2 = new NoEdgeSystem();
        SystemEntry[] entries =
        [
            EntryFor(noEdge1, after: [typeof(GraphMarker)]),
            EntryFor(noEdge2, after: [typeof(GraphMarker)]),
        ];

        var result = SystemOrderGraph.Resolve(entries);

        var markerNode = OrderNode.ForMarker(typeof(GraphMarker));
        result.Nodes.Should().ContainSingle(n => n == markerNode);
        result.Edges.Should().Contain(e => e.Before == markerNode && e.After == OrderNode.ForSystem(noEdge1));
        result.Edges.Should().Contain(e => e.Before == markerNode && e.After == OrderNode.ForSystem(noEdge2));
    }

    [Fact]
    public void EdgeTargetingAnUnregisteredType_Throws()
    {
        SystemEntry[] entries = [EntryFor(new NoEdgeSystem(), after: [typeof(GraphSystemA)])]; // GraphSystemA never registered

        var act = () => SystemOrderGraph.Resolve(entries);

        act.Should().Throw<InvalidOperationException>().WithMessage("*GraphSystemA*");
    }

    [Fact]
    public void EdgeTargetingATypeRegisteredTwice_Throws()
    {
        var duplicate1 = new GraphSystemA();
        var duplicate2 = new GraphSystemA();
        SystemEntry[] entries = [EntryFor(duplicate1), EntryFor(duplicate2), EntryFor(new NoEdgeSystem(), after: [typeof(GraphSystemA)])];

        var act = () => SystemOrderGraph.Resolve(entries);

        act.Should().Throw<InvalidOperationException>().WithMessage("*ambiguous*");
    }

    [Fact]
    public void EdgeTargetingATypeThatIsNeitherEcsSystemNorMarkerSystem_Throws()
    {
        SystemEntry[] entries = [EntryFor(new BadTargetSystem(), before: [typeof(NotASystem)])];

        var act = () => SystemOrderGraph.Resolve(entries);

        act.Should().Throw<InvalidOperationException>().WithMessage("*NotASystem*");
    }
}
