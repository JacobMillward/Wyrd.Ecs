namespace Wyrd.Ecs.Persistence.Continuous;

/// <summary>
/// One captured value change whose bytes haven't been computed yet, the deferred
/// counterpart to <see cref="CapturedWalEntry"/>. Only a
/// <see cref="WalRecordKind.ComponentChanged"/> entry is ever deferred this way;
/// structural events have no value to encode and are captured directly as
/// <see cref="CapturedWalEntry"/>. <see cref="Codec"/> resolves <see cref="Value"/> to
/// bytes at drain time, so the encode cost lands on the WAL-writer thread, not the
/// capturing thread.
/// </summary>
internal readonly record struct PendingValueChange(IComponentCodec Codec, int Tick, EntityId EntityId, object Value);
