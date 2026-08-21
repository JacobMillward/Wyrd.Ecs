using SDL3;

namespace Wyrd.Ecs.Audio;

public sealed partial class AudioSystem
{
    private readonly Dictionary<Playback, Entity> _following = new();
    private readonly Dictionary<AudioOutput, Entity> _listeners = new();
    private readonly Dictionary<AudioOutput, WorldTransform> _lastKnownListenerTransform = new();

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

    /// <summary>Sets which entity's <see cref="Transform"/> <paramref name="output"/>'s spatial
    /// playbacks are positioned relative to. Always takes an explicit <see cref="AudioOutput"/> -
    /// unlike <see cref="Follow"/>, this is persistent standing state, and there's no bus-like
    /// value here to carry the output implicitly. If the entity is later gone, playback
    /// positions simply freeze at the last known listener transform - there's no "stop" analog
    /// for a listener the way there is for a followed <see cref="Playback"/>.</summary>
    public void SetListener(AudioOutput output, Entity entity)
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        GetOutput(output); // throws if stale
        _listeners[output] = entity;
    }

    private void UpdateSpatialPlaybacks(World world)
    {
        List<Playback>? gone = null;
        foreach (var (playback, entity) in _following)
        {
            if (!world.IsAlive(entity))
            {
                (gone ??= []).Add(playback);
                continue;
            }
            var slot = GetPlaybackSlot(playback);
            var sourceTransform = world.GetInterpolatedWorldTransform(entity);
            ApplyListenerRelativePosition(world, slot, sourceTransform.Position);
        }

        if (gone is not null)
        {
            foreach (var playback in gone)
            {
                _following.Remove(playback);
                Stop(playback);
            }
        }

        foreach (var (playback, position) in _fixedPositions)
        {
            var slot = GetPlaybackSlot(playback);
            ApplyListenerRelativePosition(world, slot, position);
        }
    }

    private void ApplyListenerRelativePosition(World world, PlaybackSlot slot, System.Numerics.Vector3 sourceWorldPosition)
    {
        var output = FindOutputFor(slot.Mixer);
        WorldTransform listenerTransform;
        if (_listeners.TryGetValue(output, out var listenerEntity) && world.IsAlive(listenerEntity))
        {
            // Listener alive: use its live transform and remember it, so a later disconnect/
            // destroy freezes here instead of snapping to origin.
            listenerTransform = world.GetInterpolatedWorldTransform(listenerEntity);
            _lastKnownListenerTransform[output] = listenerTransform;
        }
        else if (!_lastKnownListenerTransform.TryGetValue(output, out listenerTransform))
        {
            // No listener was ever set for this output at all - nothing to freeze at, so this
            // is the one case where falling back to the origin, unrotated, is legitimate.
            listenerTransform = new WorldTransform(System.Numerics.Vector3.Zero, System.Numerics.Quaternion.Identity, System.Numerics.Vector3.One);
        }

        var relative = System.Numerics.Vector3.Transform(
            sourceWorldPosition - listenerTransform.Position,
            System.Numerics.Quaternion.Inverse(listenerTransform.Rotation));

        var point = new Mixer.Point3D { X = relative.X, Y = relative.Y, Z = relative.Z };
        unsafe
        {
            Mixer.SetTrack3DPosition(slot.Track, (IntPtr)(&point));
        }
    }

    private AudioOutput FindOutputFor(IntPtr mixer)
    {
        for (var i = 0; i < _outputs.Count; i++)
            if (_outputs[i]?.Mixer == mixer)
                return new AudioOutput(i, _outputGenerations[i]);
        throw new InvalidOperationException("Mixer does not belong to any live AudioOutput.");
    }
}
