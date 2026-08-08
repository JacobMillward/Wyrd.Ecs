namespace Wyrd.Ecs.Persistence;

/// <summary>
/// Excludes a component or tag type from persistence that defaults to including everything
/// automatically - every JSON or binary (MemoryPack) component, and every tag.
/// </summary>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class PersistenceIgnoreAttribute : Attribute { }
