using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wyrd.Ecs.Debug.Internal;

/// <summary>
/// Serializes <see cref="EncodedComponent"/> for API responses only - <see cref="Read"/>
/// throws, since no endpoint ever deserializes one back. Exists so
/// <see cref="EncodedComponent.Data"/> embeds as real nested JSON in the response instead
/// of <see cref="JsonSerializer"/>'s default base64-string handling of <c>byte[]</c>.
/// Assumes <c>Data</c> is JSON, guaranteed only when the registry's codecs are JSON-based
/// (true for <c>World.WithDebugServer</c>/<c>CreateDebugServer</c>'s generated overloads,
/// not for the bare <c>DebugServer(world, registry, options)</c> constructor with an
/// arbitrary registry) - <see cref="Write"/> names the offending component if that
/// assumption is ever violated, rather than surfacing a bare JSON-parse error.
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
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(value.Data);
            }
            catch (JsonException ex)
            {
                throw new JsonException(
                    $"Component '{value.Discriminator}' did not decode as JSON. " +
                    "EncodedComponentJsonConverter requires every codec in the registry " +
                    "to produce JSON bytes.", ex);
            }

            using (document) document.RootElement.WriteTo(writer);
        }

        writer.WriteEndObject();
    }
}
