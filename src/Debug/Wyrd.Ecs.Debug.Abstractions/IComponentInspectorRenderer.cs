namespace Wyrd.Ecs.Debug.Abstractions;

/// <summary>
/// Describes a component's value as an <see cref="InspectorField"/> for the inspector to
/// draw, in place of the generic JSON field grid, and applies an edit back onto a value.
/// Registered via <see cref="DebugRendererAttribute"/> and discovered by
/// <c>Wyrd.Ecs.Debug.Generators.DebugRendererRegistrationGenerator</c>.
/// </summary>
public interface IComponentInspectorRenderer<T> where T : struct, Wyrd.Ecs.IComponent
{
    /// <summary>Describes <paramref name="value"/> for display/editing.</summary>
    InspectorField Describe(T value);

    /// <summary>Applies <paramref name="edit"/> onto <paramref name="value"/>, returning the new value.</summary>
    T Apply(T value, InspectorEdit edit);
}
