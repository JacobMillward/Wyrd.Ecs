namespace Wyrd.Ecs;

/// <summary>
/// Pins a component/relation/tag's persisted discriminator explicitly, instead of the
/// generator's default (fully qualified type name). Decouples save-file identity from the
/// C# type name, so a class rename doesn't need RenamedFrom on its own.
/// </summary>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class StableNameAttribute(string name) : Attribute
{
    /// <summary>The explicit discriminator to register this type under.</summary>
    public string Name { get; } = name;
}
