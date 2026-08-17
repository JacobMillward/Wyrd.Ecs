using System.Text.Json;

namespace Wyrd.Ecs.Debug.Abstractions;

/// <summary>
/// The raw value of a custom-renderer edit, as sent by the frontend - a thin wrapper over
/// <see cref="JsonElement"/> so an <see cref="IComponentInspectorRenderer{T}.Apply"/>
/// implementation can coerce it to whatever type its field actually needs.
/// <see cref="Label"/> identifies which field of a multi-field component this edit
/// targets, matched against that field's own <see cref="InspectorField.Label"/>.
/// A single-field renderer can ignore it entirely.
/// </summary>
public readonly struct InspectorEdit(string label, JsonElement value)
{
    /// <summary>The <see cref="InspectorField"/> label this edit targets.</summary>
    public string Label => label;

    /// <summary>Coerces the edit value to an <see cref="int"/>, throwing if it isn't a JSON number.</summary>
    public int AsInt() => value.GetInt32();

    /// <summary>Coerces the edit value to a <see cref="double"/>, throwing if it isn't a JSON number.</summary>
    public double AsDouble() => value.GetDouble();

    /// <summary>Coerces the edit value to a <see cref="string"/>, throwing if it isn't a JSON string or is null.</summary>
    public string AsString() => value.GetString() ?? throw new InvalidOperationException("Edit value was null.");

    /// <summary>Coerces the edit value to a <see cref="bool"/>, throwing if it isn't a JSON boolean.</summary>
    public bool AsBool() => value.GetBoolean();
}
