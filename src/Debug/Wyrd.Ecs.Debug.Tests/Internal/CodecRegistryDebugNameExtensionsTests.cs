using Wyrd.Ecs.Debug.Internal;

namespace Wyrd.Ecs.Debug.Tests.Internal;

// DebugNameGenerator resolves this to "Wyrd.Ecs.Debug.Tests.Internal.Health"
// (namespace-qualified, since it's top-level not nested), not bare "Health", since
// DebugServerApiTests/EndToEndApiSmokeTests each declare their own unrelated "Health"
// type too.
public struct Health : IComponent { public int Current; }

public class CodecRegistryDebugNameExtensionsTests
{
    [Fact]
    public void ARegisteredType_ByItsDebugName_ResolvesTheMatchingCodec()
    {
        var registry = new CodecRegistry();
        registry.Register<Health>("Health", h => System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(h),
            b => System.Text.Json.JsonSerializer.Deserialize<Health>(b));

        var found = registry.TryGetByDebugName("Wyrd.Ecs.Debug.Tests.Internal.Health", out var codec);

        found.Should().BeTrue();
        codec.Discriminator.Should().Be("Health");
    }

    [Fact]
    public void AnUnregisteredName_ReturnsFalse()
    {
        var registry = new CodecRegistry();

        var found = registry.TryGetByDebugName("NoSuchType", out _);

        found.Should().BeFalse();
    }
}
