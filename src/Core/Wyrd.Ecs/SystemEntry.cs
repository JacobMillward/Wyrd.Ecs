namespace Wyrd.Ecs;

/// <summary>
/// One system's full registration state: how to construct it, its access footprint, its
/// declared ordering edges (both attribute- and fluent-declared union into the same
/// lists once both have had a chance to run — see the generator-emitted
/// <c>GeneratedSystemRegistrationExtensions.EdgesOrEmpty</c>), and whether it starts
/// enabled. Mutable so <see cref="SystemRegistration"/>'s
/// <c>Before</c>/<c>After</c>/<c>StartDisabled</c> chain can keep editing the same entry
/// after it's created. Public (not <c>Internal</c>) because <see cref="ISystemScheduler"/>
/// is a public interface a custom scheduler implements against this type directly.
/// </summary>
public sealed class SystemEntry
{
    /// <summary>The system's concrete type — the key every Type-keyed lookup (<see cref="World.GetSystem{T}"/>, ordering edges) resolves against.</summary>
    public required Type SystemType;

    /// <summary>Builds the instance, given the <see cref="World"/> it's being registered against (needed for a <c>ctor(World)</c> system). Called exactly once, whenever this entry is actually registered.</summary>
    public required Func<World, EcsSystem> Construct;

    /// <summary>
    /// This system's generated read/write footprint, or <c>null</c> if the generator
    /// never produced one for it (a system whose <c>Execute</c> never calls
    /// <c>.ForEach</c>/isn't a <c>QuerySystem</c> — e.g. one that only issues structural
    /// commands). <c>null</c> is not the same as an explicit empty
    /// <see cref="SystemAccess"/>: <see cref="Internal.StagePlanner"/> falls back to
    /// <see cref="IQueryAccessDescriptor"/> and then the conservative exclusive-stage
    /// default only when this is <c>null</c>, never for a real (even empty) footprint.
    /// </summary>
    public SystemAccess? Access;

    /// <summary>The constructed system, or <c>null</c> before <see cref="Construct"/> has run.</summary>
    public EcsSystem? Instance;

    /// <summary>Every type this system must run in a strictly earlier stage than, unioned from <c>[RunBefore]</c> and fluent <see cref="SystemRegistration.Before{T}"/> declarations.</summary>
    public List<Type> BeforeTargets = [];

    /// <summary>Every type this system must run in a strictly later stage than, unioned from <c>[RunAfter]</c> and fluent <see cref="SystemRegistration.After{T}"/> declarations.</summary>
    public List<Type> AfterTargets = [];

    /// <summary>Whether <see cref="EcsSystem.Enabled"/> is set to true (the default) or false the moment <see cref="Instance"/> is constructed. See <see cref="SystemRegistration.StartDisabled"/>.</summary>
    public bool StartEnabled = true;
}
