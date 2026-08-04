namespace Wyrd.Ecs;

/// <summary>
/// The value handed to every system each iteration: how much time passed since the
/// previous <see cref="World.Update"/>/<see cref="World.RunOnce"/> call (<see cref="Delta"/>),
/// and how much has passed in total since the world was created (<see cref="Elapsed"/>).
/// This is Wyrd's *virtual* clock — scaled by <see cref="World.TimeScale"/>, frozen at
/// <see cref="TimeSpan.Zero"/> delta while <see cref="World.IsPaused"/>. <see cref="World.RealTime"/>
/// is the parallel wall-clock counterpart, unaffected by either. Unrelated to
/// <see cref="World.CurrentTick"/>: that's an internal monotonic counter change-tracking
/// stamps against, not exposed to systems at all.
/// </summary>
public readonly record struct Time(TimeSpan Delta, TimeSpan Elapsed);
