namespace Wyrd.Ecs.Tests.Internal;

using Wyrd.Ecs.Internal;

file struct OrderingComponentA : IComponent;
file struct OrderingComponentB : IComponent;

file sealed class OrderedSystemP : EcsSystem { protected override void Execute(World world, Time time) { } }
file sealed class OrderedSystemQ : EcsSystem { protected override void Execute(World world, Time time) { } }
file sealed class OrderedSystemR : EcsSystem { protected override void Execute(World world, Time time) { } }
file sealed class SchedulerTestMarker : MarkerSystem { }

public class SystemSchedulerOrderingTests
{
    [Fact]
    public void EdgeWithNoDataConflict_StillForcesSeparateStages()
    {
        var access = new Dictionary<Type, SystemAccess>
        {
            [typeof(OrderedSystemP)] = new(Reads: [], Writes: [typeof(OrderingComponentA)]),
            [typeof(OrderedSystemQ)] = new(Reads: [], Writes: [typeof(OrderingComponentB)]),
        };
        var p = new OrderedSystemP();
        var q = new OrderedSystemQ();
        OrderedSystem[] systems = [p, Order.For(q).After<OrderedSystemP>()];

        var stages = SystemScheduler.BuildStages(systems, access);

        stages.Should().HaveCount(2);
        stages[0].Should().ContainSingle(s => s == p);
        stages[1].Should().ContainSingle(s => s == q);
    }

    [Fact]
    public void NoEdgeAndNoConflict_StillPackIntoOneStage()
    {
        var access = new Dictionary<Type, SystemAccess>
        {
            [typeof(OrderedSystemP)] = new(Reads: [], Writes: [typeof(OrderingComponentA)]),
            [typeof(OrderedSystemQ)] = new(Reads: [], Writes: [typeof(OrderingComponentB)]),
        };
        OrderedSystem[] systems = [new OrderedSystemP(), new OrderedSystemQ()];

        var stages = SystemScheduler.BuildStages(systems, access);

        stages.Should().ContainSingle();
        stages[0].Should().HaveCount(2);
    }

    [Fact]
    public void EdgeAndDataConflict_EachSeparatesIndependently()
    {
        // P writes A. Q writes B (no data conflict with P) but has an edge After<P>,
        // so it must land later regardless. R writes A -- conflicts with P on data
        // grounds alone, independent of any edge -- so it must land in a different
        // stage from P too, whether or not that happens to be Q's stage.
        var p = new OrderedSystemP();
        var q = Order.For(new OrderedSystemQ()).After<OrderedSystemP>();
        var r = new OrderedSystemR();
        var access = new Dictionary<Type, SystemAccess>
        {
            [typeof(OrderedSystemP)] = new(Reads: [], Writes: [typeof(OrderingComponentA)]),
            [typeof(OrderedSystemQ)] = new(Reads: [], Writes: [typeof(OrderingComponentB)]),
            [typeof(OrderedSystemR)] = new(Reads: [], Writes: [typeof(OrderingComponentA)]),
        };
        OrderedSystem[] systems = [p, q, r];

        var stages = SystemScheduler.BuildStages(systems, access);

        var pStage = stages.Single(s => s.Contains(p));
        var qStage = stages.Single(s => s.Any(sys => sys.GetType() == typeof(OrderedSystemQ)));
        var rStage = stages.Single(s => s.Any(sys => sys.GetType() == typeof(OrderedSystemR)));

        pStage.Should().NotBeSameAs(qStage);
        pStage.Should().NotBeSameAs(rStage);
    }

    [Fact]
    public void MarkerNode_NeverAppearsInTheMaterializedStages()
    {
        var x = Order.For(new OrderedSystemP()).Before<SchedulerTestMarker>();
        var y = Order.For(new OrderedSystemQ()).After<SchedulerTestMarker>();
        var access = new Dictionary<Type, SystemAccess>
        {
            [typeof(OrderedSystemP)] = new(Reads: [], Writes: [typeof(OrderingComponentA)]),
            [typeof(OrderedSystemQ)] = new(Reads: [], Writes: [typeof(OrderingComponentA)]),
        };
        OrderedSystem[] systems = [x, y];

        var stages = SystemScheduler.BuildStages(systems, access);

        // BuildStages' return type is IReadOnlyList<IReadOnlyList<EcsSystem>> -- since
        // MarkerSystem is a sibling of EcsSystem, not a subtype, a marker can never
        // appear here at all as a matter of the type system; this only needs to
        // confirm no phantom extra entry snuck in some other way.
        stages.SelectMany(s => s).Should().HaveCount(2);
    }

    [Fact]
    public void MarkerAloneInAStage_DoesNotProduceAnEmptyMaterializedStage()
    {
        var x = Order.For(new OrderedSystemP()).Before<SchedulerTestMarker>();
        var access = new Dictionary<Type, SystemAccess>
        {
            [typeof(OrderedSystemP)] = new(Reads: [], Writes: [typeof(OrderingComponentA)]),
        };
        OrderedSystem[] systems = [x];

        var stages = SystemScheduler.BuildStages(systems, access);

        // OrderedSystemP is the only real system here; the marker it points at exists
        // only to be an edge target and, with nothing registered after it, contributes
        // no stage of its own to the materialized output.
        stages.Should().ContainSingle();
        stages[0].Should().ContainSingle(s => s.GetType() == typeof(OrderedSystemP));
    }
}
