namespace Wyrd.Ecs.Tests;

public class SystemAccessTests
{
    [Fact]
    public void Constructs_WithReadsAndWrites()
    {
        var access = new SystemAccess(Reads: [typeof(int)], Writes: [typeof(float)]);

        access.Reads.Should().ContainSingle().Which.Should().Be(typeof(int));
        access.Writes.Should().ContainSingle().Which.Should().Be(typeof(float));
    }

    [Fact]
    public void Equality_IsStructural()
    {
        var a = new SystemAccess(Reads: [typeof(int)], Writes: []);
        var b = new SystemAccess(Reads: [typeof(int)], Writes: []);

        a.Should().Be(b);
    }
}
