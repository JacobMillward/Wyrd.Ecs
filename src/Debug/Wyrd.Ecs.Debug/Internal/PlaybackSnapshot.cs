namespace Wyrd.Ecs.Debug.Internal;

internal readonly record struct PlaybackSnapshot(bool IsPaused, double TimeScale);
