namespace Wyrd.Ecs.Persistence.Continuous.Internal;

/// <summary>
/// Turns entity/component/relation lifecycle events into <see cref="CapturedWalEntry"/>
/// values via <paramref name="onCaptured"/>. <see cref="OnComponentAdded"/> is a
/// deliberate no-op: <c>World.AddComponent</c> already marks storage dirty, so the next
/// <see cref="World.Subscribe(IComponentCodec)"/> scan captures the same data as an
/// ordinary value change, making a separate record redundant. Tag events are never
/// captured: tags carry no data and are already skipped by
/// <see cref="World.EnumerateAll"/> on checkpoint.
/// </summary>
internal sealed class StructuralChangeCapture(World world, CodecRegistry registry, Action<CapturedWalEntry> onCaptured) : IStructuralChangeObserver
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

    public void OnRelationLinked(Entity source, Entity target, int typeIndex)
    {
        if (!registry.TryGetRelationByTypeIndex(typeIndex, out var codec)) return;
        var payload = codec.EncodeEdge(world, source, target);
        onCaptured(new CapturedWalEntry(WalRecordKind.RelationLinked, world.CurrentTick, world.GetPermanentId(source), codec.Discriminator, codec.SchemaHash, payload, world.GetPermanentId(target)));
    }

    public void OnRelationUnlinked(Entity source, Entity target, int typeIndex)
    {
        if (!registry.TryGetRelationByTypeIndex(typeIndex, out var codec)) return;
        onCaptured(new CapturedWalEntry(WalRecordKind.RelationUnlinked, world.CurrentTick, world.GetPermanentId(source), codec.Discriminator, codec.SchemaHash, [], world.GetPermanentId(target)));
    }
}
