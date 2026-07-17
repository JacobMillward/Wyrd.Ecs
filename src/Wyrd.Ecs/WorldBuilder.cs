namespace Wyrd.Ecs;

/// <summary>
/// Constructs a <see cref="World"/>. Currently equivalent to <c>new World()</c> with
/// no options set; exists as the entry point future construction-time configuration
/// (such as registering Systems) will extend.
/// </summary>
public sealed class WorldBuilder
{
    /// <summary>Builds a new <see cref="World"/>.</summary>
    public World Build() => new();
}
