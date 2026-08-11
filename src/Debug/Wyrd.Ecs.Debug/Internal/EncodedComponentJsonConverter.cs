using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wyrd.Ecs.Debug.Internal;

/// <summary>
/// Serializes <see cref="EncodedComponent"/> for API responses only - <see cref="Read"/>
/// throws, since no endpoint ever deserializes one back. Exists purely so
/// <see cref="EncodedComponent.Data"/> (already JSON bytes, since
/// <c>World.WithDebugServer</c>/<c>CreateDebugServer</c> always populate the registry via
/// the JSON persistence package) embeds as real nested JSON in the response instead of
/// <see cref="JsonSerializer"/>'s default base64-string handling of <c>byte[]</c>.
/// </summary>
internal sealed class EncodedComponentJsonConverter : JsonConverter<EncodedComponent>
{
    public override EncodedComponent Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException("EncodedComponent is only ever serialized for API responses, never read back.");

    public override void Write(Utf8JsonWriter writer, EncodedComponent value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WritePropertyName("entity");
        JsonSerializer.Serialize(writer, value.Entity, options);

        writer.WriteString("discriminator", value.Discriminator);

        writer.WritePropertyName("data");
        if (value.Data.Length == 0)
        {
            writer.WriteNullValue();
        }
        else
        {
            using var document = JsonDocument.Parse(value.Data);
            document.RootElement.WriteTo(writer);
        }

        writer.WriteEndObject();
    }
}
