using Wyrd.Ecs.Debug.Abstractions;

namespace Wyrd.Ecs.Debug.Internal;

/// <summary>
/// Publishes a <see cref="WorldSnapshot"/> once per tick, gated by whether anyone is
/// subscribed to <see cref="Changed"/> - the same subscription a caller needs anyway to
/// learn a new snapshot exists, so it doubles as the "is anyone listening" signal with no
/// separate connection-tracking API. Deliberately not a registered <c>EcsSystem</c>: a
/// full-world walk declares no query access, so the parallel scheduler has no way to know
/// it needs to run exclusive of every other system; registering it as one would risk it
/// running concurrently with a mid-tick mutation, exactly the hazard
/// <see cref="World.EnumerateArchetypes()"/>/<see cref="World.EnumerateEntities()"/>'s own
/// eager materialization exists to avoid. <see cref="World.OnTickAdvanced"/> is already
/// the safe point that guarantee assumes: after every system for the tick has run and
/// commands are applied, before the next tick's mutations start.
/// </summary>
internal sealed class SnapshotPublisher(World world, CodecRegistry registry)
{
    private WorldSnapshot? _latest;

    public event Action? Changed;

    public WorldSnapshot? Latest => _latest;

    public void OnTickAdvanced(int tick)
    {
        if (Changed is null) return;

        var entities = world.EnumerateEntities(registry);
        var inspected = new InspectedEntity[entities.Count];
        for (var i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];
            var components = new InspectedComponent[entity.Components.Count];
            for (var j = 0; j < entity.Components.Count; j++)
            {
                var component = entity.Components[j];
                components[j] = new InspectedComponent(component, DescribeIfRendered(component), Wyrd.Ecs.Internal.SystemManagedRegistry.IsManaged(component.Discriminator));
            }
            inspected[i] = new InspectedEntity(entity.Entity, components, entity.Tags);
        }

        var snapshot = new WorldSnapshot(world.EnumerateArchetypes(), inspected);
        Interlocked.Exchange(ref _latest, snapshot);
        Changed?.Invoke();
    }

    // Runs only for components whose discriminator has a registered [DebugRenderer];
    // most components have none, so this is a bounded addition to the full-snapshot walk
    // this method already does every tick, not a new order of complexity.
    private InspectorField? DescribeIfRendered(EncodedComponent component)
    {
        if (!DebugRendererRegistry.TryGetRenderer(component.Discriminator, out var renderer)) return null;
        if (!registry.TryGetByDebugName(component.Discriminator, out var codec)) return null;
        return renderer.Describe(codec.DecodeValue(component.Data));
    }
}
