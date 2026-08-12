using Wyrd.Ecs.Debug.Abstractions;

namespace Wyrd.Ecs.Debug.Tests;

public class DebugRendererRegistryTests
{
    [Fact]
    public void ARegisteredName_ResolvesItsDescribeAndApplyDelegates()
    {
        DebugRendererRegistry.Register("Test.Widget",
            value => new InspectorField.ReadOnly("x", value.ToString() ?? ""),
            (value, edit) => edit.AsInt());

        var found = DebugRendererRegistry.TryGetRenderer("Test.Widget", out var registration);

        found.Should().BeTrue();
        registration.Describe(42).Should().BeOfType<InspectorField.ReadOnly>();
    }

    [Fact]
    public void AnUnregisteredName_ReturnsFalse()
    {
        var found = DebugRendererRegistry.TryGetRenderer("Test.NoSuchWidget", out _);

        found.Should().BeFalse();
    }
}
