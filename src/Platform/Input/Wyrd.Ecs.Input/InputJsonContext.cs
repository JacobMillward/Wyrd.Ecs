using System.Text.Json.Serialization;

namespace Wyrd.Ecs.Input;

[JsonSerializable(typeof(BindingFileDto))]
internal sealed partial class InputJsonContext : JsonSerializerContext
{
}
