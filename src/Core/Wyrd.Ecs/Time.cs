namespace Wyrd.Ecs;

/// <summary>
/// The value handed to every system each iteration: how much time passed since the
/// previous <see cref="World.Tick"/>/<see cref="World.RunOnce"/> call (<see cref="Delta"/>),
/// and how much has passed in total since the world was created (<see cref="Elapsed"/>).
/// Unrelated to <see cref="World.CurrentTick"/> — that's an internal monotonic counter
/// change-tracking stamps against, not exposed to systems at all.
/// </summary>
public readonly record struct Time(TimeSpan Delta, TimeSpan Elapsed);
