using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Audio;

public sealed partial class AudioSystem
{
    private EventReader<DeviceChange>? _deviceChanges;

    private void EnsureDeviceChangeReader(World world) => _deviceChanges ??= world.CreateEventReader<DeviceChange>();

    private void ApplyDeviceChanges()
    {
        if (_deviceChanges is null) return;
        foreach (var change in _deviceChanges.Read())
        {
            if (change.DeviceKind != DeviceKind.AudioOutput || change.Change != DeviceChangeKind.Disconnected) continue;
            // Every AudioOutput already guards its own use via GetOutput's stale-handle check;
            // there's no SDL device id -> AudioOutput mapping kept today (AddOutput doesn't
            // record which raw deviceId it was given), so there's nothing further to look up
            // and invalidate here yet.
        }
    }
}
