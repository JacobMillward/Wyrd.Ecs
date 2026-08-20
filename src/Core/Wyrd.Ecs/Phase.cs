namespace Wyrd.Ecs;

/// <summary>
/// Where in the tick a system runs, relative to the untagged majority. <see cref="Update"/>
/// (the default - declared first so <c>default(Phase)</c> equals it) needs no declaration
/// at all; <see cref="PreUpdate"/>/<see cref="PostUpdate"/> are sugar for
/// <c>[RunBefore(typeof(StartOfUpdatePhase))]</c>/<c>[RunAfter(typeof(EndOfUpdatePhase))]</c>
/// - see <see cref="PhaseAttribute"/> and <see cref="SystemRegistration.Phase"/>.
/// </summary>
public enum Phase
{
    /// <summary>No special ordering - the common case. Declaring this explicitly is a no-op, identical to declaring nothing.</summary>
    Update,

    /// <summary>Runs before every <see cref="Update"/>/<see cref="PostUpdate"/> system, with no edge needed from anything else.</summary>
    PreUpdate,

    /// <summary>Runs after every <see cref="Update"/>/<see cref="PreUpdate"/> system, with no edge needed from anything else.</summary>
    PostUpdate,
}
