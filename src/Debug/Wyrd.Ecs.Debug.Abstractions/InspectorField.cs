using System.Text.Json.Serialization;

namespace Wyrd.Ecs.Debug.Abstractions;

/// <summary>
/// A closed set of field kinds a custom <see cref="IComponentInspectorRenderer{T}"/>
/// composes to describe how to edit a component - declared here in C#, drawn by a fixed
/// set of functions in the frontend, never as arbitrary markup. Serializes with a
/// <c>"kind"</c> discriminator via <see cref="JsonPolymorphicAttribute"/>.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(Slider), "slider")]
[JsonDerivedType(typeof(Number), "number")]
[JsonDerivedType(typeof(Text), "text")]
[JsonDerivedType(typeof(Checkbox), "checkbox")]
[JsonDerivedType(typeof(ReadOnly), "readOnly")]
[JsonDerivedType(typeof(Group), "group")]
public abstract record InspectorField(string Label)
{
    /// <summary>A draggable numeric range, bounded by <see cref="Min"/>/<see cref="Max"/>.</summary>
    public sealed record Slider(string Label, double Value, double Min, double Max) : InspectorField(Label);

    /// <summary>A free-form numeric input, no bounds.</summary>
    public sealed record Number(string Label, double Value) : InspectorField(Label);

    /// <summary>A free-form string input.</summary>
    public sealed record Text(string Label, string Value) : InspectorField(Label);

    /// <summary>A boolean toggle.</summary>
    public sealed record Checkbox(string Label, bool Value) : InspectorField(Label);

    /// <summary>A display-only value with no edit control.</summary>
    public sealed record ReadOnly(string Label, string Value) : InspectorField(Label);

    /// <summary>Nests <see cref="Children"/> under one label.</summary>
    public sealed record Group(string Label, IReadOnlyList<InspectorField> Children) : InspectorField(Label);
}
