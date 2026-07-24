namespace Wyrd.Ecs.Persistence.Json;

/// <summary>
/// Marks an <see cref="IComponent"/> type to be excluded from the JSON codec's
/// otherwise fully-automatic inclusion policy. Every <see cref="IComponent"/> type is
/// included in JSON saves by default — this is the one lever to opt a specific type
/// back out.
/// </summary>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class JsonPersistenceIgnoreAttribute : Attribute { }
