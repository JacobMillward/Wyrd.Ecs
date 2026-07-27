namespace Wyrd.Ecs.Tests;

file struct Config : IComponent;

file sealed class DynamicConfigSystem : EcsSystem, IQueryAccessDescriptor
{
    protected override void OnUpdate(World world, Time time) { }
    public SystemAccess DescribeAccess() => new(Reads: [], Writes: [typeof(Config)]);
}

public class QueryAccessDescriptorTests
{
    [Fact]
    public void HandWrittenSystem_CanDescribeItsOwnAccess()
    {
        var system = new DynamicConfigSystem();
        var access = system.DescribeAccess();

        access.Writes.Should().ContainSingle().Which.Should().Be(typeof(Config));
        access.Reads.Should().BeEmpty();
    }
}
