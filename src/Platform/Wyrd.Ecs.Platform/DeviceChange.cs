namespace Wyrd.Ecs.Platform;

/// <summary>
/// A keyboard or mouse connecting or disconnecting. Emitted exactly once per real
/// hot-plug by <see cref="PlatformSystem"/>, the single canonical source - subscribe via
/// <c>World.CreateEventReader&lt;DeviceChange&gt;()</c>. One event type covering both
/// directions, not two, so a reader can't handle connects while forgetting disconnects.
/// </summary>
public readonly record struct DeviceChange(uint DeviceId, DeviceKind DeviceKind, DeviceChangeKind Change) : IEvent;

/// <summary>What kind of physical device a <see cref="DeviceChange"/> refers to.</summary>
public enum DeviceKind
{
    /// <summary>A keyboard.</summary>
    Keyboard,

    /// <summary>A mouse.</summary>
    Mouse,
}

/// <summary>Whether a <see cref="DeviceChange"/> is a connection or a disconnection.</summary>
public enum DeviceChangeKind
{
    /// <summary>The device connected.</summary>
    Connected,

    /// <summary>The device disconnected.</summary>
    Disconnected,
}
