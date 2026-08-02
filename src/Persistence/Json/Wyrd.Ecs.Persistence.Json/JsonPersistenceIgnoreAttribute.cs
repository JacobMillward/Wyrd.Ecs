namespace Wyrd.Ecs.Persistence.Json;

/// <summary>
/// Excludes a component type from JSON saves. By default every component type is
/// saved automatically; add this attribute to a component struct to leave it out.
/// </summary>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class JsonPersistenceIgnoreAttribute : Attribute { }
