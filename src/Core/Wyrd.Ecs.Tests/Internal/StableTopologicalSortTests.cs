namespace Wyrd.Ecs.Tests.Internal;

using Wyrd.Ecs.Internal;

file sealed class NodeA : EcsSystem { protected override void Execute(World world, Time time) { } }
file sealed class NodeB : EcsSystem { protected override void Execute(World world, Time time) { } }
file sealed class NodeC : EcsSystem { protected override void Execute(World world, Time time) { } }

public class StableTopologicalSortTests
{
    [Fact]
    public void NoEdges_OrderMatchesTieBreakExactly()
    {
        var a = new NodeA();
        var b = new NodeB();
        var tieBreak = new Dictionary<SchedulableSystem, int> { [b] = 0, [a] = 1 };

        var order = StableTopologicalSort.Sort([b, a], [], tieBreak);

        order.Should().Equal(b, a);
    }

    [Fact]
    public void OneEdge_BeforeNodePrecedesAfterNodeRegardlessOfTieBreak()
    {
        var a = new NodeA();
        var b = new NodeB();
        // Tie-break alone would put b first; the edge must override that.
        var tieBreak = new Dictionary<SchedulableSystem, int> { [b] = 0, [a] = 1 };
        SystemOrderGraph.Edge[] edges = [new(a, b)];

        var order = StableTopologicalSort.Sort([b, a], edges, tieBreak);

        order.Should().Equal(a, b);
    }

    [Fact]
    public void DirectCycle_ThrowsNamingBothNodes()
    {
        var a = new NodeA();
        var b = new NodeB();
        var tieBreak = new Dictionary<SchedulableSystem, int> { [a] = 0, [b] = 1 };
        SystemOrderGraph.Edge[] edges = [new(a, b), new(b, a)];

        var act = () => StableTopologicalSort.Sort([a, b], edges, tieBreak);

        act.Should().Throw<InvalidOperationException>().WithMessage("*NodeA*NodeB*");
    }

    [Fact]
    public void LongerCycle_Throws()
    {
        var a = new NodeA();
        var b = new NodeB();
        var c = new NodeC();
        var tieBreak = new Dictionary<SchedulableSystem, int> { [a] = 0, [b] = 1, [c] = 2 };
        SystemOrderGraph.Edge[] edges = [new(a, b), new(b, c), new(c, a)];

        var act = () => StableTopologicalSort.Sort([a, b, c], edges, tieBreak);

        act.Should().Throw<InvalidOperationException>();
    }
}
