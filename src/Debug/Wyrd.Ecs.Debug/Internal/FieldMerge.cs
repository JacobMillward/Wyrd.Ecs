using System.Text.Json;

namespace Wyrd.Ecs.Debug.Internal;

/// <summary>
/// Replaces one named field's value inside an already-JSON-encoded component's bytes,
/// leaving every other field untouched. The edit path for the generic field grid: each
/// keystroke sends one field, this merges it against the component's last-known snapshot
/// data before <see cref="IComponentCodec.DecodeInto"/> applies the result.
/// </summary>
internal static class FieldMerge
{
    public static byte[] MergeField(byte[] originalData, string field, JsonElement newValue)
    {
        using var document = JsonDocument.Parse(originalData);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            var replaced = false;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                writer.WritePropertyName(property.Name);
                if (property.Name == field)
                {
                    newValue.WriteTo(writer);
                    replaced = true;
                }
                else
                {
                    property.Value.WriteTo(writer);
                }
            }
            if (!replaced)
            {
                writer.WritePropertyName(field);
                newValue.WriteTo(writer);
            }
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }
}
