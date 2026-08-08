namespace Wyrd.Ecs.Persistence;

/// <summary>
/// Excludes a component or tag type from persistence that defaults to including everything
/// automatically (JSON components, all tags). MemoryPack components need no equivalent -
/// [MemoryPackable]'s absence already excludes a component there.
/// </summary>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class PersistenceIgnoreAttribute : Attribute { }
