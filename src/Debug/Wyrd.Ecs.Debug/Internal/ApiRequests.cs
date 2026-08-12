using System.Text.Json;

namespace Wyrd.Ecs.Debug.Internal;

internal sealed record FieldEditRequest(string Field, JsonElement Value);

internal sealed record SetTimeScaleRequest(double Value);
