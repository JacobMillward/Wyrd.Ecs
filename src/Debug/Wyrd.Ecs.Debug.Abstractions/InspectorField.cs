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
    public sealed record Slider(string Label, double Value, double Min, double Max) : InspectorField(Label);

    public sealed record Number(string Label, double Value) : InspectorField(Label);

    public sealed record Text(string Label, string Value) : InspectorField(Label);

    public sealed record Checkbox(string Label, bool Value) : InspectorField(Label);

    public sealed record ReadOnly(string Label, string Value) : InspectorField(Label);

    public sealed record Group(string Label, IReadOnlyList<InspectorField> Children) : InspectorField(Label);
}
