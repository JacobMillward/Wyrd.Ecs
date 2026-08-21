namespace Wyrd.Ecs.Audio;

public sealed partial class AudioSystem
{
    private readonly Dictionary<Playback, Entity> _following = new();

    /// <summary>Tracks <paramref name="entity"/>'s <see cref="Transform"/> for
    /// <paramref name="playback"/> every tick from now on. If <paramref name="entity"/> is no
    /// longer alive on a later tick, <paramref name="playback"/> is stopped - <c>Follow</c>
    /// explicitly ties a playback's identity to that entity's continued existence, so a caller
    /// who wants a sound to persist after its trigger entity is gone should use a fixed
    /// <c>position</c> on <c>Play</c> instead of calling this at all.</summary>
    public void Follow(Playback playback, Entity entity)
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        GetPlaybackSlot(playback); // throws if stale, same guard as every other accessor
        _following[playback] = entity;
    }

    private void UpdateFollowedPlaybacks(World world)
    {
        List<Playback>? gone = null;
        foreach (var (playback, entity) in _following)
        {
            if (!world.IsAlive(entity))
            {
                (gone ??= []).Add(playback);
                continue;
            }
            _ = world.GetInterpolatedWorldTransform(entity);
            // Listener-relative position math lands in Task 11, alongside SetListener - this
            // task only wires up the tracking/gone-cleanup half.
        }

        if (gone is null) return;
        foreach (var playback in gone)
        {
            _following.Remove(playback);
            Stop(playback);
        }
    }
}
