using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs.Tests;

public class DebugNameRegistryTests
{
    private struct Marker : IComponent { }
    private struct OtherMarker : IComponent { }

    [Fact]
    public void RegisteredType_ResolvesByTypeIndex()
    {
        DebugNameRegistry.Register<Marker>("Marker");

        DebugNameRegistry.TryGetName(TypeIndex<Marker>.Value, out var name).Should().BeTrue();
        name.Should().Be("Marker");
    }

    [Fact]
    public void UnregisteredType_DoesNotResolve()
    {
        DebugNameRegistry.TryGetName(TypeIndex<OtherMarker>.Value, out var name).Should().BeFalse();
        name.Should().BeNull();
    }

    [Fact]
    public void TwoTypes_CanShareTheSameName_NoCollisionGuard()
    {
        DebugNameRegistry.Register<Marker>("Same");
        var act = () => DebugNameRegistry.Register<OtherMarker>("Same");

        act.Should().NotThrow();
    }
}
