using System.Text.Json;

namespace Wyrd.Ecs.Debug.Abstractions;

/// <summary>
/// The raw value of a custom-renderer edit, as sent by the frontend - a thin wrapper over
/// <see cref="JsonElement"/> so an <see cref="IComponentInspectorRenderer{T}.Apply"/>
/// implementation can coerce it to whatever type its field actually needs.
/// </summary>
public readonly struct InspectorEdit(JsonElement value)
{
    public int AsInt() => value.GetInt32();

    public double AsDouble() => value.GetDouble();

    public string AsString() => value.GetString() ?? throw new InvalidOperationException("Edit value was null.");

    public bool AsBool() => value.GetBoolean();
}
