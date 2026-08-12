using System.Text.Json;
using Wyrd.Ecs.Debug.Internal;

namespace Wyrd.Ecs.Debug.Tests.Internal;

public class EncodedComponentJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new EncodedComponentJsonConverter() },
    };

    [Fact]
    public void AComponentWithData_SerializesTheDataAsEmbeddedJsonNotBase64()
    {
        var component = new EncodedComponent(new Entity(5, 1), "Health",
            null, JsonSerializer.SerializeToUtf8Bytes(new { Current = 7 }));

        var json = JsonSerializer.Serialize(component, Options);

        // Data's casing is whatever the component's own codec encoded, copied verbatim -
        // the outer camelCase policy applies to entity/discriminator/data, not into
        // already-encoded component bytes it never generated itself.
        json.Should().Contain("\"Current\":7");
        json.Should().NotContain("=="); // no base64 padding anywhere in the output
    }

    [Fact]
    public void AComponentWithEmptyData_SerializesDataAsNull()
    {
        var component = new EncodedComponent(new Entity(5, 1), "Marker", null, []);

        var json = JsonSerializer.Serialize(component, Options);

        json.Should().Contain("\"data\":null");
    }

    [Fact]
    public void TheEntityFieldRoundTripsIdAndGeneration()
    {
        var component = new EncodedComponent(new Entity(5, 2), "Health", null, "{}"u8.ToArray());

        var json = JsonSerializer.Serialize(component, Options);

        json.Should().Contain("\"id\":5");
        json.Should().Contain("\"generation\":2");
    }

    [Fact]
    public void AComponentWithNonJsonData_ThrowsAnErrorNamingTheActualCause()
    {
        // A registry populated with a non-JSON codec (e.g. Wyrd.Ecs.Persistence.Binary's
        // MemoryPack-encoded ones) would produce bytes like this, not JSON.
        var component = new EncodedComponent(new Entity(5, 1), "Health", null, [0xFF, 0x00, 0x01]);

        var act = () => JsonSerializer.Serialize(component, Options);

        act.Should().Throw<JsonException>().WithMessage("*Health*");
    }
}
