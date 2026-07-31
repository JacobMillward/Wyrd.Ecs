namespace Wyrd.Ecs.Tests;

public class ParentHierarchyTests
{
    [Fact]
    public void Parent_IsExclusive()
    {
        // Fully qualified, not the `Internal.X` shorthand: this file's namespace is plain
        // `Wyrd.Ecs.Tests`, which also has its own `Wyrd.Ecs.Tests.Internal` sibling
        // namespace (housing e.g. RelationTraitsTests) -- unqualified `Internal` would
        // resolve to that one, not `Wyrd.Ecs.Internal`. See existing precedent in
        // AccessorTests.cs/ComponentCodecRegistryTests.cs, which do the same.
        Wyrd.Ecs.Internal.RelationTraits<Parent>.IsExclusive.Should().BeTrue();
    }

    [Fact]
    public void SecondParent_ReplacesTheFirst()
    {
        var world = new World();
        Entity child = world.Commands.CreateEntity();
        Entity momOne = world.Commands.CreateEntity();
        Entity momTwo = world.Commands.CreateEntity();
        world.Commands.AddRelation<Parent>(child, momOne);
        world.ApplyCommands();

        world.Commands.AddRelation<Parent>(child, momTwo);
        world.ApplyCommands();

        world.Targets<Parent>(child).Should().HaveCount(1);
        world.Targets<Parent>(child).Should().ContainKey(momTwo);
        world.HasRelation<Parent>(child, momOne).Should().BeFalse();
    }

    [Fact]
    public void DestroyingAParent_RecursivelyDestroysTheWholeSubtree()
    {
        var world = new World();
        Entity root = world.Commands.CreateEntity();
        Entity arm = world.Commands.CreateEntity();
        Entity hand = world.Commands.CreateEntity();
        Entity sword = world.Commands.CreateEntity();
        world.Commands.AddRelation<Parent>(arm, root);
        world.Commands.AddRelation<Parent>(hand, arm);
        world.Commands.AddRelation<Parent>(sword, hand);
        world.ApplyCommands();

        world.Commands.DestroyEntity(arm);
        world.ApplyCommands();

        world.IsAlive(arm).Should().BeFalse();
        world.IsAlive(hand).Should().BeFalse();
        world.IsAlive(sword).Should().BeFalse();
        world.IsAlive(root).Should().BeTrue();
    }

    [Fact]
    public void DestroyingAParent_WithMultipleChildren_DestroysEveryChildsSubtree()
    {
        var world = new World();
        Entity parent = world.Commands.CreateEntity();
        Entity childA = world.Commands.CreateEntity();
        Entity childB = world.Commands.CreateEntity();
        Entity grandchildOfA = world.Commands.CreateEntity();
        world.Commands.AddRelation<Parent>(childA, parent);
        world.Commands.AddRelation<Parent>(childB, parent);
        world.Commands.AddRelation<Parent>(grandchildOfA, childA);
        world.ApplyCommands();

        world.Commands.DestroyEntity(parent);
        world.ApplyCommands();

        world.IsAlive(childA).Should().BeFalse();
        world.IsAlive(childB).Should().BeFalse();
        world.IsAlive(grandchildOfA).Should().BeFalse();
    }

    [Fact]
    public void TryGetParent_HasParent_ReturnsTrueAndTheParent()
    {
        var world = new World();
        Entity child = world.Commands.CreateEntity();
        Entity parent = world.Commands.CreateEntity();
        world.Commands.AddRelation<Parent>(child, parent);
        world.ApplyCommands();

        world.TryGetParent(child, out var found).Should().BeTrue();
        found.Should().Be(parent);
    }

    [Fact]
    public void TryGetParent_NoParent_ReturnsFalse()
    {
        var world = new World();
        Entity child = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.TryGetParent(child, out _).Should().BeFalse();
    }

    [Fact]
    public void GetParent_HasParent_ReturnsIt()
    {
        var world = new World();
        Entity child = world.Commands.CreateEntity();
        Entity parent = world.Commands.CreateEntity();
        world.Commands.AddRelation<Parent>(child, parent);
        world.ApplyCommands();

        world.GetParent(child).Should().Be(parent);
    }

    [Fact]
    public void GetParent_NoParent_Throws()
    {
        var world = new World();
        Entity child = world.Commands.CreateEntity();
        world.ApplyCommands();

        var act = () => world.GetParent(child);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Children_ReturnsEveryDirectChild()
    {
        var world = new World();
        Entity parent = world.Commands.CreateEntity();
        Entity childA = world.Commands.CreateEntity();
        Entity childB = world.Commands.CreateEntity();
        world.Commands.AddRelation<Parent>(childA, parent);
        world.Commands.AddRelation<Parent>(childB, parent);
        world.ApplyCommands();

        world.Children(parent).Should().BeEquivalentTo([childA, childB]);
    }

    [Fact]
    public void Ancestors_YieldsClosestParentFirstUpToTheRoot()
    {
        var world = new World();
        Entity root = world.Commands.CreateEntity();
        Entity arm = world.Commands.CreateEntity();
        Entity hand = world.Commands.CreateEntity();
        world.Commands.AddRelation<Parent>(arm, root);
        world.Commands.AddRelation<Parent>(hand, arm);
        world.ApplyCommands();

        world.Ancestors(hand).Should().Equal([arm, root]);
    }

    [Fact]
    public void Descendants_YieldsDepthFirstPreOrder()
    {
        var world = new World();
        Entity root = world.Commands.CreateEntity();
        Entity arm = world.Commands.CreateEntity();
        Entity hand = world.Commands.CreateEntity();
        world.Commands.AddRelation<Parent>(arm, root);
        world.Commands.AddRelation<Parent>(hand, arm);
        world.ApplyCommands();

        world.Descendants(root).Should().Equal([arm, hand]);
    }

    [Fact]
    public void SetParent_QueuesTheEdge_VisibleAfterApplyCommands()
    {
        var world = new World();
        Entity child = world.Commands.CreateEntity();
        Entity parent = world.Commands.CreateEntity();
        world.ApplyCommands();

        world[child].SetParent(parent);
        world.ApplyCommands();

        world.GetParent(child).Should().Be(parent);
    }

    [Fact]
    public void ClearParent_WithAParent_RemovesTheEdge()
    {
        var world = new World();
        Entity child = world.Commands.CreateEntity();
        Entity parent = world.Commands.CreateEntity();
        world.Commands.AddRelation<Parent>(child, parent);
        world.ApplyCommands();

        world[child].ClearParent();
        world.ApplyCommands();

        world.TryGetParent(child, out _).Should().BeFalse();
    }

    [Fact]
    public void ClearParent_WithNoParent_IsANoOp()
    {
        var world = new World();
        Entity child = world.Commands.CreateEntity();
        world.ApplyCommands();

        var act = () => { world[child].ClearParent(); world.ApplyCommands(); };

        act.Should().NotThrow();
    }

    [Fact]
    public void AddChild_QueuesTheEdgeFromTheParentsSide()
    {
        var world = new World();
        Entity parent = world.Commands.CreateEntity();
        Entity child = world.Commands.CreateEntity();
        world.ApplyCommands();

        world[parent].AddChild(child);
        world.ApplyCommands();

        world.GetParent(child).Should().Be(parent);
    }

    [Fact]
    public void RemoveChild_RemovesOnlyThatChildsEdge()
    {
        var world = new World();
        Entity parent = world.Commands.CreateEntity();
        Entity childA = world.Commands.CreateEntity();
        Entity childB = world.Commands.CreateEntity();
        world.Commands.AddRelation<Parent>(childA, parent);
        world.Commands.AddRelation<Parent>(childB, parent);
        world.ApplyCommands();

        world[parent].RemoveChild(childA);
        world.ApplyCommands();

        world.TryGetParent(childA, out _).Should().BeFalse();
        world.GetParent(childB).Should().Be(parent);
    }
}
