namespace Wyrd.Ecs;

/// <summary>
/// Marks a component struct as written by an engine system, not authored content, e.g.
/// <see cref="PreviousTransform"/>. Picked up by <c>Wyrd.Ecs.Generators.DebugNameGenerator</c>
/// and registered into <see cref="Internal.SystemManagedRegistry"/>, which the debug
/// inspector consults to group these components separately from ones a caller added
/// directly.
/// </summary>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class SystemManagedAttribute : Attribute;
