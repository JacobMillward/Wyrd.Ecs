namespace Wyrd.Ecs;

/// <summary>
/// A chainable handle to one system's registration, returned by <c>AddSystem&lt;T&gt;()</c>
/// on <see cref="WorldBuilder"/> or <see cref="World"/>. <see cref="Before{T}"/>/
/// <see cref="After{T}"/>/<see cref="StartDisabled"/> edit the specific entry this
/// registration wraps; <see cref="RegisterNext"/> (called only by generator-emitted
/// <c>AddSystem&lt;T&gt;()</c> overloads on this type) continues the chain onto whatever
/// registered this one in the first place, so a fluent expression never has to break out
/// to re-reference the original <see cref="WorldBuilder"/>/<see cref="World"/>.
/// <see cref="Build"/> lets a <see cref="WorldBuilder"/>-originated chain end right where
/// the last registration left off (<c>builder.AddSystem&lt;A&gt;().AddSystem&lt;B&gt;().Build()</c>);
/// it's unavailable (throws) on a chain that started from a live <see cref="World"/>,
/// which already exists and has nothing left to build.
/// </summary>
public sealed class SystemRegistration
{
    private readonly Func<Type, SystemAccess?, Func<World, EcsSystem>, IReadOnlyList<Type>, IReadOnlyList<Type>, SystemCadence, SystemEntry> _register;
    private readonly Func<World>? _build;
    private readonly SystemEntry _entry;

    /// <summary>Test-only introspection (reachable within the core solution via <c>InternalsVisibleTo</c>) - never referenced by generated code, which lives in an arbitrary consumer assembly with no such grant.</summary>
    internal SystemEntry Entry => _entry;

    internal SystemRegistration(
        Func<Type, SystemAccess?, Func<World, EcsSystem>, IReadOnlyList<Type>, IReadOnlyList<Type>, SystemCadence, SystemEntry> register,
        Func<World>? build,
        SystemEntry entry)
    {
        _register = register;
        _build = build;
        _entry = entry;
    }

    /// <summary>Adds "must run before an instance/synthesized marker of <typeparamref name="T"/>."</summary>
    public SystemRegistration Before<T>() where T : SchedulableSystem
    {
        _entry.BeforeTargets.Add(typeof(T));
        return this;
    }

    /// <summary>Adds "must run after an instance/synthesized marker of <typeparamref name="T"/>."</summary>
    public SystemRegistration After<T>() where T : SchedulableSystem
    {
        _entry.AfterTargets.Add(typeof(T));
        return this;
    }

    /// <summary>
    /// Sugar for <see cref="Before{T}"/>/<see cref="After{T}"/> targeting
    /// <see cref="StartOfUpdatePhase"/>/<see cref="EndOfUpdatePhase"/> - the fluent
    /// equivalent of <see cref="PhaseAttribute"/>, for a registration site (rather than a
    /// class declaration) to declare which <see cref="Wyrd.Ecs.Phase"/> this system runs
    /// in. <see cref="Phase.Update"/> is a genuine no-op (adds no edge), the same as
    /// omitting this call entirely. This is the only way a generic <c>EcsSystem</c> can
    /// participate in <c>Phase.PreUpdate</c>/<c>Phase.PostUpdate</c> - see
    /// <see cref="PhaseAttribute"/>'s own doc comment for why the attribute can't.
    /// </summary>
    public SystemRegistration Phase(Phase phase) => phase switch
    {
        Wyrd.Ecs.Phase.PreUpdate => Before<StartOfUpdatePhase>(),
        Wyrd.Ecs.Phase.PostUpdate => After<EndOfUpdatePhase>(),
        Wyrd.Ecs.Phase.Update => this,
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null),
    };

    /// <summary>This system starts with <see cref="EcsSystem.Enabled"/> false.</summary>
    public SystemRegistration StartDisabled()
    {
        _entry.StartEnabled = false;
        return this;
    }

    /// <summary>
    /// Registers another system via the same registrar this registration came from -
    /// what every generator-emitted <c>AddSystem&lt;T&gt;()</c> overload on
    /// <see cref="SystemRegistration"/> itself calls, so a fluent chain like
    /// <c>builder.AddSystem&lt;A&gt;().AddSystem&lt;B&gt;()</c> keeps registering onto the
    /// same builder/world without ever needing to re-reference it by name.
    /// <paramref name="generatedBeforeTargets"/>/<paramref name="generatedAfterTargets"/>
    /// seed the new entry's edges, and <paramref name="cadence"/> seeds its
    /// <see cref="SystemEntry.Cadence"/>, the same way <see cref="WorldBuilder.AddSystemCore"/>'s
    /// own parameters do.
    /// </summary>
    public SystemRegistration RegisterNext(
        Type systemType,
        SystemAccess? access,
        Func<World, EcsSystem> construct,
        IReadOnlyList<Type> generatedBeforeTargets,
        IReadOnlyList<Type> generatedAfterTargets,
        SystemCadence cadence = SystemCadence.Variable) =>
        new(_register, _build, _register(systemType, access, construct, generatedBeforeTargets, generatedAfterTargets, cadence));

    /// <summary>
    /// Finishes a <see cref="WorldBuilder"/>-originated chain - equivalent to calling
    /// <see cref="WorldBuilder.Build"/> directly on the builder this registration (and
    /// every one before it in the chain) came from. Throws if this registration
    /// originated from a live <see cref="World"/> instead (nothing to build - the
    /// system is already registered and running).
    /// </summary>
    public World Build() =>
        _build?.Invoke() ?? throw new InvalidOperationException(
            "This SystemRegistration came from a live World, not a WorldBuilder - there is nothing to Build(). The system is already registered.");
}
