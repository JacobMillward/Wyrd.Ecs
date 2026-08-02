namespace Wyrd.Ecs.Internal;

/// <summary>
/// One system's full registration state: how to construct it, its access footprint, its
/// declared ordering edges (both attribute- and fluent-declared union into the same
/// lists once both have had a chance to run — see the generator-emitted <c>Seed</c>
/// helper in <c>GeneratedSystemRegistrationExtensions</c>), and whether it starts
/// enabled. Mutable so <see cref="Wyrd.Ecs.SystemRegistration"/>'s
/// <c>Before</c>/<c>After</c>/<c>StartDisabled</c> chain can keep editing the same entry
/// after it's created.
/// </summary>
internal sealed class SystemEntry
{
    public required Type SystemType;
    public required Func<World, EcsSystem> Construct;

    /// <summary>
    /// This system's generated read/write footprint, or <c>null</c> if the generator
    /// never produced one for it (a system whose <c>Execute</c> never calls
    /// <c>.ForEach</c>/isn't a <c>QuerySystem</c> — e.g. one that only issues structural
    /// commands). <c>null</c> is not the same as an explicit empty
    /// <see cref="Wyrd.Ecs.SystemAccess"/>: <see cref="Internal.StagePlanner"/> falls
    /// back to <see cref="IQueryAccessDescriptor"/> and then the conservative
    /// exclusive-stage default only when this is <c>null</c>, never for a real (even
    /// empty) footprint.
    /// </summary>
    public SystemAccess? Access;

    public EcsSystem? Instance;
    public List<Type> BeforeTargets = [];
    public List<Type> AfterTargets = [];
    public bool StartEnabled = true;
}
