using System.Collections.Generic;

namespace Wyrd.Ecs;

/// <summary>
/// System registry and scheduling: runtime registration/removal against the
/// <see cref="ISystemScheduler"/> computed at <see cref="WorldBuilder.Build"/> time, and the
/// out-of-schedule <see cref="RunOnce"/> harness entry point.
/// </summary>
public sealed partial class World
{
    private readonly ISystemScheduler _executor;
    private TimeSpan _totalElapsed;

    /// <summary>Runs <paramref name="system"/> once, outside the normal schedule (a harness/test convenience). Advances <see cref="CurrentTick"/> the same way <see cref="Update"/> does.</summary>
    public void RunOnce(EcsSystem system, TimeSpan delta)
    {
        AdvanceTick();
        _totalElapsed += delta;
        system.InvokeExecute(this, new Time(delta, _totalElapsed));
    }

    /// <summary>
    /// Registers one system against this already-running <see cref="World"/>, constructing
    /// it immediately (so <see cref="GetSystem{T}"/> reflects it right away) but deferring
    /// its stage placement to the next <see cref="Update"/> call - see
    /// <see cref="ISystemScheduler"/>. Not called directly by consumer code - the generator
    /// emits a strongly-typed <c>AddSystem&lt;T&gt;()</c> overload closing over this, the
    /// same way <see cref="WorldBuilder.AddSystemCore"/> does for the build-time path.
    /// Returns a chainable <see cref="SystemRegistration"/> for declaring ordering edges;
    /// <see cref="SystemRegistration.Build"/> is unavailable on the result (there's nothing
    /// to build - this <see cref="World"/> already exists and is already running).
    /// </summary>
    public SystemRegistration AddSystemCore(
        Type systemType,
        SystemAccess? access,
        Func<World, EcsSystem> construct,
        IReadOnlyList<Type> generatedBeforeTargets,
        IReadOnlyList<Type> generatedAfterTargets,
        SystemCadence cadence = SystemCadence.Variable)
    {
        var entry = new SystemEntry { SystemType = systemType, Construct = construct, Access = access, Cadence = cadence };
        entry.BeforeTargets.AddRange(generatedBeforeTargets);
        entry.AfterTargets.AddRange(generatedAfterTargets);
        return _executor.Register(entry, this);
    }

    /// <summary>
    /// Forces an immediate recompute if the schedule is currently dirty from a runtime
    /// <see cref="AddSystemCore"/>/<see cref="RemoveSystem(EcsSystem)"/> call - otherwise a
    /// no-op. <see cref="Update"/> already does this automatically at the start of every
    /// tick; call this directly right after a batch of runtime registrations if you want
    /// a bad edge (naming a type that never registered), a cycle, or an ambiguous target
    /// to throw immediately, at this call site, instead of waiting for the next <see cref="Update"/>.
    /// </summary>
    public void FlushSystemChanges() => _executor.Flush();

    /// <summary>The live instance registered for <typeparamref name="T"/>. Throws if none is registered - use <see cref="TryGetSystem{T}"/> if that's expected.</summary>
    public T GetSystem<T>() where T : EcsSystem =>
        _executor.Find(typeof(T)) as T ?? throw new InvalidOperationException($"No system of type {typeof(T)} is registered.");

    /// <summary>Same as <see cref="GetSystem{T}"/>, without throwing when nothing is registered.</summary>
    public bool TryGetSystem<T>(out T? system) where T : EcsSystem
    {
        system = _executor.Find(typeof(T)) as T;
        return system is not null;
    }

    /// <summary>Removes the registered <typeparamref name="T"/>, calling its <see cref="EcsSystem.OnDestroy"/> hook exactly once. Returns false if none was registered.</summary>
    public bool RemoveSystem<T>() where T : EcsSystem =>
        _executor.Find(typeof(T)) is EcsSystem system && RemoveSystem(system);

    /// <summary>Removes <paramref name="system"/>, calling its <see cref="EcsSystem.OnDestroy"/> hook exactly once. Returns false if it wasn't registered (already removed, or never was).</summary>
    public bool RemoveSystem(EcsSystem system)
    {
        if (!_executor.Remove(system)) return false;
        system.InvokeOnDestroy();
        return true;
    }
}
