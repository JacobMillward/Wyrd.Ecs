namespace Wyrd.Ecs;

/// <summary>
/// Declares that the annotated system must run in a strictly earlier stage than
/// <see cref="Target"/> (an <see cref="EcsSystem"/> or <see cref="MarkerSystem"/>
/// type). Stackable: a class may carry more than one, one per target. Not inherited:
/// a subclass of an annotated system does not pick up its base class's edges; each
/// class states its own. Read once via reflection at <c>WorldBuilder.Build()</c>,
/// never per tick.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class RunBeforeAttribute(Type target) : Attribute
{
    /// <summary>The <see cref="EcsSystem"/> or <see cref="MarkerSystem"/> type this system must run in a strictly earlier stage than.</summary>
    public Type Target { get; } = target;
}
