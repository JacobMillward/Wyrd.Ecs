
using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs.Tests.Internal;

file sealed class PhaseSystemA : EcsSystem { protected override void Execute(World world, Time time) { } }
file sealed class PhaseSystemB : EcsSystem { protected override void Execute(World world, Time time) { } }
file sealed class PhaseSystemC : EcsSystem { protected override void Execute(World world, Time time) { } }

public class PhaseMarkerTests
{
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

    private static int StageIndexOf(IReadOnlyList<IReadOnlyList<EcsSystem>> stages, EcsSystem system) =>
        Enumerable.Range(0, stages.Count).First(i => stages[i].Contains(system));

    [Fact]
    public void PreUpdateSystem_RunsBeforeAnUntaggedSystem()
    {
        var early = new PhaseSystemA();
        var ordinary = new PhaseSystemB();
        SystemEntry[] entries = [EntryFor(early, before: [typeof(StartOfUpdatePhase)]), EntryFor(ordinary)];

        var stages = StagePlanner.BuildStages(entries);

        StageIndexOf(stages, early).Should().BeLessThan(StageIndexOf(stages, ordinary));
    }

    [Fact]
    public void PostUpdateSystem_RunsAfterAnUntaggedSystem()
    {
        var late = new PhaseSystemA();
        var ordinary = new PhaseSystemB();
        SystemEntry[] entries = [EntryFor(late, after: [typeof(EndOfUpdatePhase)]), EntryFor(ordinary)];

        var stages = StagePlanner.BuildStages(entries);

        StageIndexOf(stages, late).Should().BeGreaterThan(StageIndexOf(stages, ordinary));
    }

    [Fact]
    public void PreAndPostUpdateSystems_BracketEveryUntaggedSystemAtOnce()
    {
        var early = new PhaseSystemA();
        var late = new PhaseSystemB();
        var ordinary = new PhaseSystemC();
        SystemEntry[] entries =
        [
            EntryFor(early, before: [typeof(StartOfUpdatePhase)]),
            EntryFor(late, after: [typeof(EndOfUpdatePhase)]),
            EntryFor(ordinary),
        ];

        var stages = StagePlanner.BuildStages(entries);

        StageIndexOf(stages, early).Should().BeLessThan(StageIndexOf(stages, ordinary));
        StageIndexOf(stages, ordinary).Should().BeLessThan(StageIndexOf(stages, late));
    }

    [Fact]
    public void TwoPreUpdateSystems_KeepTheirOwnExplicitRelativeOrder()
    {
        var platformLike = new PhaseSystemA();
        var intentLike = new PhaseSystemB();
        SystemEntry[] entries =
        [
            EntryFor(platformLike, before: [typeof(StartOfUpdatePhase)]),
            EntryFor(intentLike, before: [typeof(StartOfUpdatePhase)], after: [typeof(PhaseSystemA)]),
        ];

        var stages = StagePlanner.BuildStages(entries);

        StageIndexOf(stages, platformLike).Should().BeLessThan(StageIndexOf(stages, intentLike));
    }

    [Fact]
    public void NoSystemReferencesEitherMarker_ProducesNoSyntheticEdgesAtAll()
    {
        SystemEntry[] entries = [EntryFor(new PhaseSystemA()), EntryFor(new PhaseSystemB())];

        var result = SystemOrderGraph.Resolve(entries);

        result.Nodes.Should().NotContain(OrderNode.ForMarker(typeof(StartOfUpdatePhase)),
            "the gate must keep the synthesis off entirely when nothing references the markers");
        result.Nodes.Should().NotContain(OrderNode.ForMarker(typeof(EndOfUpdatePhase)));
        result.Edges.Should().BeEmpty();
    }

    [Fact]
    public void OnlyPreUpdateReferenced_StillProducesBothMarkerNodes()
    {
        // Regression guard: the fixed StartOfUpdatePhase -> EndOfUpdatePhase bridging edge
        // references both markers even when only one was ever declared by an entry - both
        // must land in Nodes or the topological sort throws on a dangling edge target.
        SystemEntry[] entries = [EntryFor(new PhaseSystemA(), before: [typeof(StartOfUpdatePhase)])];

        var result = SystemOrderGraph.Resolve(entries);

        result.Nodes.Should().Contain(OrderNode.ForMarker(typeof(StartOfUpdatePhase)));
        result.Nodes.Should().Contain(OrderNode.ForMarker(typeof(EndOfUpdatePhase)));
    }

    [Fact]
    public void ContradictoryPhaseAndExplicitEdge_ThrowsANamedCycleNotSilentMisbehavior()
    {
        // A PreUpdate system that's also explicitly declared After a PostUpdate system is
        // unsatisfiable - this is the exact composition risk raised during design, and the
        // answer is: the existing cycle detection already catches it, nothing new needed.
        var preUpdateSystem = new PhaseSystemA();
        var postUpdateSystem = new PhaseSystemB();
        SystemEntry[] entries =
        [
            EntryFor(preUpdateSystem, before: [typeof(StartOfUpdatePhase)], after: [typeof(PhaseSystemB)]),
            EntryFor(postUpdateSystem, after: [typeof(EndOfUpdatePhase)]),
        ];

        var act = () => StagePlanner.BuildStages(entries);

        act.Should().Throw<InvalidOperationException>().WithMessage("*cycle*");
    }
}
