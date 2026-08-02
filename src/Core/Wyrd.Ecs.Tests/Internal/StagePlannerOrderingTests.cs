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
    private static SystemEntry EntryFor(EcsSystem instance, SystemAccess access, IReadOnlyList<Type>? before = null, IReadOnlyList<Type>? after = null) =>
        new()
        {
            SystemType = instance.GetType(),
            Construct = _ => instance,
            Access = access,
            Instance = instance,
            BeforeTargets = before is null ? [] : [.. before],
            AfterTargets = after is null ? [] : [.. after],
        };

    [Fact]
    public void EdgeWithNoDataConflict_StillForcesSeparateStages()
    {
        var p = new OrderedSystemP();
        var q = new OrderedSystemQ();
        SystemEntry[] entries =
        [
            EntryFor(p, new(Reads: [], Writes: [typeof(OrderingComponentA)])),
            EntryFor(q, new(Reads: [], Writes: [typeof(OrderingComponentB)]), after: [typeof(OrderedSystemP)]),
        ];

        var stages = StagePlanner.BuildStages(entries);

        stages.Should().HaveCount(2);
        stages[0].Should().ContainSingle(s => s == p);
        stages[1].Should().ContainSingle(s => s == q);
    }

    [Fact]
    public void NoEdgeAndNoConflict_StillPackIntoOneStage()
    {
        SystemEntry[] entries =
        [
            EntryFor(new OrderedSystemP(), new(Reads: [], Writes: [typeof(OrderingComponentA)])),
            EntryFor(new OrderedSystemQ(), new(Reads: [], Writes: [typeof(OrderingComponentB)])),
        ];

        var stages = StagePlanner.BuildStages(entries);

        stages.Should().ContainSingle();
        stages[0].Should().HaveCount(2);
    }

    [Fact]
    public void EdgeAndDataConflict_EachSeparatesIndependently()
    {
        // Q has no data conflict with P but an explicit After<P> edge; R has a data conflict
        // with P but no edge. Both must still separate from P, via different mechanisms.
        var p = new OrderedSystemP();
        SystemEntry[] entries =
        [
            EntryFor(p, new(Reads: [], Writes: [typeof(OrderingComponentA)])),
            EntryFor(new OrderedSystemQ(), new(Reads: [], Writes: [typeof(OrderingComponentB)]), after: [typeof(OrderedSystemP)]),
            EntryFor(new OrderedSystemR(), new(Reads: [], Writes: [typeof(OrderingComponentA)])),
        ];

        var stages = StagePlanner.BuildStages(entries);

        var pStage = stages.Single(s => s.Contains(p));
        var qStage = stages.Single(s => s.Any(sys => sys.GetType() == typeof(OrderedSystemQ)));
        var rStage = stages.Single(s => s.Any(sys => sys.GetType() == typeof(OrderedSystemR)));

        pStage.Should().NotBeSameAs(qStage);
        pStage.Should().NotBeSameAs(rStage);
    }

    [Fact]
    public void ThreeHopChain_AllLandInStrictlyIncreasingStages()
    {
        SystemEntry[] entries =
        [
            EntryFor(new OrderedSystemP(), new(Reads: [], Writes: [typeof(OrderingComponentA)])),
            EntryFor(new OrderedSystemQ(), new(Reads: [], Writes: [typeof(OrderingComponentB)]), after: [typeof(OrderedSystemP)]),
            EntryFor(new OrderedSystemR(), new(Reads: [], Writes: [typeof(OrderingComponentA)]), after: [typeof(OrderedSystemQ)]),
        ];

        var stages = StagePlanner.BuildStages(entries);

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
        SystemEntry[] entries =
        [
            EntryFor(new OrderedSystemP(), new(Reads: [], Writes: [typeof(OrderingComponentA)]), before: [typeof(SchedulerTestMarker)]),
            EntryFor(new OrderedSystemQ(), new(Reads: [], Writes: [typeof(OrderingComponentA)]), after: [typeof(SchedulerTestMarker)]),
        ];

        var stages = StagePlanner.BuildStages(entries);

        stages.SelectMany(s => s).Should().HaveCount(2, "MarkerSystem is a sibling of EcsSystem, not a subtype, so a marker can never appear here as a matter of the type system; this only confirms no phantom entry snuck in some other way");
    }

    [Fact]
    public void MarkerAloneInAStage_DoesNotProduceAnEmptyMaterializedStage()
    {
        SystemEntry[] entries =
        [
            EntryFor(new OrderedSystemP(), new(Reads: [], Writes: [typeof(OrderingComponentA)]), before: [typeof(SchedulerTestMarker)]),
        ];

        var stages = StagePlanner.BuildStages(entries);

        stages.Should().ContainSingle("the marker is only an edge target, and with nothing registered after it, it contributes no stage of its own to the materialized output");
        stages[0].Should().ContainSingle(s => s.GetType() == typeof(OrderedSystemP));
    }
}
