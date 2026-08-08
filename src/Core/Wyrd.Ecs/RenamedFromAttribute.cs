namespace Wyrd.Ecs;

/// <summary>
/// Declares a discriminator this type was previously saved under, so existing data still
/// resolves after a rename. Repeatable for a type renamed more than once - each occurrence
/// becomes its own alias to the current discriminator.
/// </summary>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
public sealed class RenamedFromAttribute(string oldDiscriminator) : Attribute
{
    /// <summary>A discriminator this type was previously saved under.</summary>
    public string OldDiscriminator { get; } = oldDiscriminator;
}
