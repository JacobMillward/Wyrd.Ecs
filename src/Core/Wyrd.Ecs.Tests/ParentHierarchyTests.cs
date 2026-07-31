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
        var child = world.Commands.CreateEntity();
        var momOne = world.Commands.CreateEntity();
        var momTwo = world.Commands.CreateEntity();
        world.Commands.AddRelation<Parent>(child, momOne);
        world.ApplyCommands();

        world.Commands.AddRelation<Parent>(child, momTwo);
        world.ApplyCommands();

        world.Targets<Parent>(child).Should().HaveCount(1);
        world.Targets<Parent>(child).Should().ContainKey(momTwo);
        world.HasRelation<Parent>(child, momOne).Should().BeFalse();
    }
}
