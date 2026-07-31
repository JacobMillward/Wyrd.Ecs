namespace Wyrd.Ecs;

/// <summary>
/// Declares that the annotated system must run in a strictly later stage than
/// <see cref="Target"/> (an <see cref="EcsSystem"/> or <see cref="MarkerSystem"/>
/// type). Stackable — a class may carry more than one, one per target. Read once via
/// reflection at <c>WorldBuilder.Build()</c>, never per tick.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class RunAfterAttribute(Type target) : Attribute
{
    /// <summary>The <see cref="EcsSystem"/> or <see cref="MarkerSystem"/> type this system must run in a strictly later stage than.</summary>
    public Type Target { get; } = target;
}
