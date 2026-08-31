using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs;

/// <summary>
/// Archetype-storage engine: entities with identical component/tag sets share one
/// <see cref="Archetype"/>; adding or removing a component/tag moves the entity to a
/// different archetype.
/// </summary>
/// <remarks>
/// The World is a <c>partial</c> class, one file per concern. This file is the core:
/// construction and the deferred-mutation command surface. The rest:
/// <list type="bullet">
/// <item><c>World.Archetypes.cs</c> — archetype registry, chunk queries, archetype-set caches</item>
/// <item><c>World.Entities.cs</c> — id table, liveness, reserve/place, destroy + reentrancy guard</item>
/// <item><c>World.Components.cs</c> — component/tag CRUD and archetype edge moves</item>
/// <item><c>World.Relations.cs</c> — relation edges: links/backlinks, exclusive replace, queries</item>
/// <item><c>World.Clock.cs</c> — ticks, time scale/pause, fixed-step update loop</item>
/// <item><c>World.Systems.cs</c> — runtime system registration/removal, RunOnce</item>
/// <item><c>World.Persistence.cs</c> — full-world enumeration walks for save/checkpoint</item>
/// <item><c>World.Changes.cs</c> — structural observers and change tracking</item>
/// <item><c>World.Events.cs</c> — typed event channels</item>
/// <item><c>World.Hierarchy.cs</c> — parent/child helpers over the Parent relation</item>
/// <item><c>World.Transform.cs</c> — transform propagation</item>
/// <item><c>World.Resources.cs</c> — resource storage</item>
/// <item><c>World.Debug.cs</c> — debug introspection</item>
/// <item><c>World.Run.cs</c> — the Exit event, RequestExit, and the Run main loop</item>
/// </list>
/// Each partial owns the fields only it gives meaning to; cross-concern access stays legal
/// (one class) but a concern's state lives in its concern's file.
/// </remarks>
public sealed partial class World
{
    private readonly CommandBuffer _commands;

    /// <summary>Creates a new, empty world with <see cref="DefaultArchetypeCapacity"/> and a default 1/60s fixed timestep. Use <see cref="WorldBuilder"/> to configure it.</summary>
    public World() : this(DefaultArchetypeCapacity, new ParallelSystemScheduler(1000), TimeSpan.FromSeconds(1.0 / 60.0), 5) { }

    internal World(int archetypeCapacity, ISystemScheduler executor, TimeSpan fixedStep, int maxSubstepsPerUpdate)
    {
        // Both callers already validate these; checked again here since the invariant belongs
        // on the constructor, not on trusting every call site.
        if (fixedStep <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(fixedStep), fixedStep, "Fixed timestep must be positive.");
        if (maxSubstepsPerUpdate <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxSubstepsPerUpdate), maxSubstepsPerUpdate, "maxSubstepsPerUpdate must be positive.");

        _archetypeCapacity = archetypeCapacity;
        _emptyArchetype = new Archetype(TypeBitSet.Empty, archetypeCapacity);
        _archetypes[TypeBitSet.Empty] = _emptyArchetype;
        _commands = new CommandBuffer(this);
        _executor = executor;
        _fixedStep = fixedStep;
        _maxSubstepsPerUpdate = maxSubstepsPerUpdate;
    }

    /// <summary>The built-in deferred-mutation buffer for structural changes. See <see cref="CommandBuffer"/>.</summary>
    public CommandBuffer Commands => _commands;

    /// <summary>
    /// Creates an additional <see cref="CommandBuffer"/> bound to this world, independent of
    /// <see cref="Commands"/>. Each buffer is single-writer, so several concurrent sources can
    /// queue structural changes lock-free by using their own buffer, then applying them via
    /// <see cref="ApplyCommands(CommandBuffer)"/> in whatever order the caller chooses.
    /// </summary>
    public CommandBuffer CreateCommands() => new(this);

    /// <summary>A non-storable, world-scoped bound view over <paramref name="entity"/>. See <see cref="EntityView"/>.</summary>
    public EntityView this[Entity entity] => new(this, Commands, entity);

    /// <summary>Applies every command queued on <see cref="Commands"/>, in queued order, then clears the queue.</summary>
    public void ApplyCommands() => ApplyCommands(_commands);

    /// <summary>Applies every command queued on <paramref name="commands"/>, in queued order, then clears its queue. <paramref name="commands"/> may be <see cref="Commands"/> or any buffer from <see cref="CreateCommands"/>. Throws if it was created for a different <see cref="World"/>.</summary>
    public void ApplyCommands(CommandBuffer commands)
    {
        if (commands.World != this)
            throw new InvalidOperationException("This CommandBuffer was created for a different World.");

        commands.Apply();
        _entityTable.FlushReservations();
    }
}
