
using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs.Tests.Internal;

file sealed class NodeA : EcsSystem { protected override void Execute(World world, Time time) { } }
file sealed class NodeB : EcsSystem { protected override void Execute(World world, Time time) { } }
file sealed class NodeC : EcsSystem { protected override void Execute(World world, Time time) { } }
file sealed class NodeD : EcsSystem { protected override void Execute(World world, Time time) { } }

public class StableTopologicalSortTests
{
    [Fact]
    public void NoEdges_OrderMatchesTieBreakExactly()
    {
        var a = OrderNode.ForSystem(new NodeA());
        var b = OrderNode.ForSystem(new NodeB());
        var tieBreak = new Dictionary<OrderNode, int> { [b] = 0, [a] = 1 };

        var order = StableTopologicalSort.Sort([b, a], [], tieBreak, n => n.DisplayName);

        order.Should().Equal(b, a);
    }

    [Fact]
    public void OneEdge_BeforeNodePrecedesAfterNodeRegardlessOfTieBreak()
    {
        var a = OrderNode.ForSystem(new NodeA());
        var b = OrderNode.ForSystem(new NodeB());
        // Tie-break alone would put b first; the edge must override that.
        var tieBreak = new Dictionary<OrderNode, int> { [b] = 0, [a] = 1 };
        StableTopologicalSort.Edge<OrderNode>[] edges = [new(a, b)];

        var order = StableTopologicalSort.Sort([b, a], edges, tieBreak, n => n.DisplayName);

        order.Should().Equal(a, b);
    }

    [Fact]
    public void DirectCycle_ThrowsNamingBothNodes()
    {
        var a = OrderNode.ForSystem(new NodeA());
        var b = OrderNode.ForSystem(new NodeB());
        var tieBreak = new Dictionary<OrderNode, int> { [a] = 0, [b] = 1 };
        StableTopologicalSort.Edge<OrderNode>[] edges = [new(a, b), new(b, a)];

        var act = () => StableTopologicalSort.Sort([a, b], edges, tieBreak, n => n.DisplayName);

        act.Should().Throw<InvalidOperationException>().WithMessage("*NodeA*NodeB*");
    }

    [Fact]
    public void LongerCycle_Throws()
    {
        var a = OrderNode.ForSystem(new NodeA());
        var b = OrderNode.ForSystem(new NodeB());
        var c = OrderNode.ForSystem(new NodeC());
        var tieBreak = new Dictionary<OrderNode, int> { [a] = 0, [b] = 1, [c] = 2 };
        StableTopologicalSort.Edge<OrderNode>[] edges = [new(a, b), new(b, c), new(c, a)];

        var act = () => StableTopologicalSort.Sort([a, b, c], edges, tieBreak, n => n.DisplayName);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CycleWithALeafDependent_ThrowsNamingTheCycleWithoutCrashingOnTheDependent()
    {
        // D depends on A but nothing depends on D, so it's stuck without being part of
        // the A/B cycle. Listing D first forces cycle-path reconstruction to start from
        // a non-cycle node and recover, rather than crashing on D's empty successor list.
        var a = OrderNode.ForSystem(new NodeA());
        var b = OrderNode.ForSystem(new NodeB());
        var d = OrderNode.ForSystem(new NodeD());
        var tieBreak = new Dictionary<OrderNode, int> { [d] = 0, [a] = 1, [b] = 2 };
        StableTopologicalSort.Edge<OrderNode>[] edges = [new(a, b), new(b, a), new(a, d)];

        var act = () => StableTopologicalSort.Sort([d, a, b], edges, tieBreak, n => n.DisplayName);

        act.Should().Throw<InvalidOperationException>().WithMessage("*NodeA*NodeB*");
    }
}
