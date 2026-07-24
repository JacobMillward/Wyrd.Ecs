namespace Wyrd.Ecs.Persistence.Continuous.Internal;

/// <summary>
/// Turns entity/component lifecycle events into <see cref="CapturedWalEntry"/> values
/// via <paramref name="onCaptured"/>. Only <see cref="OnEntityCreated"/>,
/// <see cref="OnEntityDestroyed"/>, and <see cref="OnComponentRemoved"/> produce
/// anything — <see cref="OnComponentAdded"/> is a deliberate no-op, since
/// <c>World.AddComponent</c> already marks a tracked type's storage dirty on add, so the
/// next <see cref="IComponentCodec.EncodeChanges"/> scan already captures the same data
/// as an ordinary value change; a separate <c>ComponentAdded</c> WAL record would be
/// redundant. Tag events are never captured — tags carry no data and are already
/// skipped by <see cref="World.EnumerateAll"/> on checkpoint, and continuous persistence
/// stays consistent with that policy.
/// </summary>
internal sealed class StructuralChangeCapture(World world, ComponentCodecRegistry registry, Action<CapturedWalEntry> onCaptured) : IStructuralChangeObserver
{
    public void OnEntityCreated(Entity entity) =>
        onCaptured(new CapturedWalEntry(WalRecordKind.EntityCreated, world.CurrentTick, world.GetPermanentId(entity), "", null, []));

    public void OnEntityDestroyed(Entity entity) =>
        onCaptured(new CapturedWalEntry(WalRecordKind.EntityDestroyed, world.CurrentTick, world.GetPermanentId(entity), "", null, []));

    public void OnComponentAdded(Entity entity, int typeIndex) { }

    public void OnComponentRemoved(Entity entity, int typeIndex)
    {
        if (!registry.TryGetByTypeIndex(typeIndex, out var codec)) return;
        onCaptured(new CapturedWalEntry(WalRecordKind.ComponentRemoved, world.CurrentTick, world.GetPermanentId(entity), codec.Discriminator, codec.SchemaHash, []));
    }

    public void OnTagAdded(Entity entity, int typeIndex) { }

    public void OnTagRemoved(Entity entity, int typeIndex) { }
}
