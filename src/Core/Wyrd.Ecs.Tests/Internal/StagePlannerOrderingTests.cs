namespace Wyrd.Ecs.Tests.Internal;

using Wyrd.Ecs.Internal;

file struct OrderingComponentA : IComponent;
file struct OrderingComponentB : IComponent;

file sealed class OrderedSystemP : EcsSystem { protected override void Execute(World world, Time time) { } }
file sealed class OrderedSystemQ : EcsSystem { protected override void Execute(World world, Time time) { } }
file sealed class OrderedSystemR : EcsSystem { protected override void Execute(World world, Time time) { } }
file sealed class SchedulerTestMarker : MarkerSystem { }

public class StagePlannerOrderingTests
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

        var stages = StagePlanner.BuildStages(systems, access);

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

        var stages = StagePlanner.BuildStages(systems, access);

        stages.Should().ContainSingle();
        stages[0].Should().HaveCount(2);
    }

    [Fact]
    public void EdgeAndDataConflict_EachSeparatesIndependently()
    {
        // Q has no data conflict with P but an explicit After<P> edge; R has a data conflict
        // with P but no edge. Both must still separate from P, via different mechanisms.
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

        var stages = StagePlanner.BuildStages(systems, access);

        var pStage = stages.Single(s => s.Contains(p));
        var qStage = stages.Single(s => s.Any(sys => sys.GetType() == typeof(OrderedSystemQ)));
        var rStage = stages.Single(s => s.Any(sys => sys.GetType() == typeof(OrderedSystemR)));

        pStage.Should().NotBeSameAs(qStage);
        pStage.Should().NotBeSameAs(rStage);
    }

    [Fact]
    public void ThreeHopChain_AllLandInStrictlyIncreasingStages()
    {
        var p = new OrderedSystemP();
        var q = Order.For(new OrderedSystemQ()).After<OrderedSystemP>();
        var r = Order.For(new OrderedSystemR()).After<OrderedSystemQ>();
        var access = new Dictionary<Type, SystemAccess>
        {
            [typeof(OrderedSystemP)] = new(Reads: [], Writes: [typeof(OrderingComponentA)]),
            [typeof(OrderedSystemQ)] = new(Reads: [], Writes: [typeof(OrderingComponentB)]),
            [typeof(OrderedSystemR)] = new(Reads: [], Writes: [typeof(OrderingComponentA)]),
        };
        OrderedSystem[] systems = [p, q, r];

        var stages = StagePlanner.BuildStages(systems, access);

        int StageIndexOf(Type systemType) =>
            Enumerable.Range(0, stages.Count).First(i => stages[i].Any(s => s.GetType() == systemType));

        var pIndex = StageIndexOf(typeof(OrderedSystemP));
        var qIndex = StageIndexOf(typeof(OrderedSystemQ));
        var rIndex = StageIndexOf(typeof(OrderedSystemR));

        pIndex.Should().BeLessThan(qIndex);
        qIndex.Should().BeLessThan(rIndex);
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

        var stages = StagePlanner.BuildStages(systems, access);

        stages.SelectMany(s => s).Should().HaveCount(2, "MarkerSystem is a sibling of EcsSystem, not a subtype, so a marker can never appear here as a matter of the type system; this only confirms no phantom entry snuck in some other way");
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

        var stages = StagePlanner.BuildStages(systems, access);

        stages.Should().ContainSingle("the marker is only an edge target, and with nothing registered after it, it contributes no stage of its own to the materialized output");
        stages[0].Should().ContainSingle(s => s.GetType() == typeof(OrderedSystemP));
    }
}
