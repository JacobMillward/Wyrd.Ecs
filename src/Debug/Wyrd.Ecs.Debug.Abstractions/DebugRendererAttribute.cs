namespace Wyrd.Ecs.Debug.Abstractions;

/// <summary>
/// Marks a component struct as having a custom inspector renderer. <paramref name="rendererType"/>
/// must implement <see cref="IComponentInspectorRenderer{T}"/> for the attributed type and have
/// a public parameterless constructor. Wrap both this usage and the renderer implementation in
/// <c>#if DEBUG</c> in Release/published gameplay code - see the package README.
/// </summary>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class DebugRendererAttribute(Type rendererType) : Attribute
{
    /// <summary>The <c>IComponentInspectorRenderer&lt;T&gt;</c> implementation to render this component with.</summary>
    public Type RendererType { get; } = rendererType;
}
