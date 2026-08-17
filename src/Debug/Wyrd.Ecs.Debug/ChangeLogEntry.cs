namespace Wyrd.Ecs.Debug;

/// <summary>
/// One structural change, as recorded by <see cref="Internal.ChangeLogRecorder"/>.
/// <see cref="ComponentName"/> is null for <see cref="ChangeKind.EntityCreated"/>/
/// <see cref="ChangeKind.EntityDestroyed"/> (no single component involved), and for a
/// type <see cref="Wyrd.Ecs.Internal.DebugNameRegistry"/> has no entry for. No originating-system
/// field yet: that's a separate, future change touching the scheduler and CommandBuffer,
/// needing its own careful no-hot-path-allocation benchmarking, not part of this UI.
/// </summary>
public readonly record struct ChangeLogEntry(int Tick, ChangeKind Kind, Entity Entity, string? ComponentName);
