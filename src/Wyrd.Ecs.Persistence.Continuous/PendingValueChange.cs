namespace Wyrd.Ecs.Persistence.Continuous;

/// <summary>
/// One captured value change whose bytes haven't been computed yet — the deferred
/// counterpart to <see cref="CapturedWalEntry"/>. Only a <see cref="WalRecordKind.ComponentChanged"/>
/// entry is ever deferred this way; a structural event (create/destroy/remove) has no
/// value to encode and is captured directly as a <see cref="CapturedWalEntry"/> instead.
/// <see cref="Codec"/> resolves <see cref="Value"/> to bytes via
/// <see cref="IComponentCodec.EncodeValue"/> at drain time, in
/// <see cref="Internal.WalSegmentWriter.WriteRecords"/> — not before, so the encode
/// cost lands on the WAL-writer thread, not the thread that captured the change.
/// </summary>
internal readonly record struct PendingValueChange(IComponentCodec Codec, int Tick, EntityId EntityId, object Value);
