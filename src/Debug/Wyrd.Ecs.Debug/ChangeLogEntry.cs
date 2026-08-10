namespace Wyrd.Ecs.Debug;

/// <summary>
/// One structural change, as recorded by <see cref="Internal.ChangeLogRecorder"/>. No
/// originating-system field yet: that's a separate, future change touching the
/// scheduler and CommandBuffer, needing its own careful no-hot-path-allocation
/// benchmarking, not part of this backend.
/// </summary>
public readonly record struct ChangeLogEntry(int Tick, ChangeKind Kind, Entity Entity, string? Discriminator);
