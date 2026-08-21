namespace Wyrd.Ecs.Platform;

/// <summary>
/// An SDL input device instance id (keyboard or mouse). Wraps the raw <see langword="uint"/>
/// so device-keyed collections like <see cref="ConnectedDevices"/> document their key instead
/// of exposing a bare number. No implicit conversion to/from <see langword="uint"/>: construct
/// one explicitly at the point a raw SDL id crosses into typed code.
/// </summary>
public readonly record struct DeviceId(uint Value);

/// <summary>
/// A keyboard or mouse connecting or disconnecting. Emitted exactly once per real
/// hot-plug by <see cref="PlatformSystem"/>, the single canonical source. Subscribe via
/// <c>World.CreateEventReader&lt;DeviceChange&gt;()</c>. One event type covering both
/// directions, not two, so a reader can't handle connects while forgetting disconnects.
/// </summary>
public readonly record struct DeviceChange(DeviceId DeviceId, DeviceKind DeviceKind, DeviceChangeKind Change) : IEvent;

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

/// <summary>
/// The live set of keyboards and mice currently connected, kept in sync by
/// <see cref="PlatformSystem"/>. Read via <c>World.GetResource&lt;ConnectedDevices&gt;()</c>;
/// no <see cref="PlatformSystem"/> reference needed. Backed by a dictionary rather than a list,
/// so add, remove, and lookup are all O(1); only a full enumeration is O(n).
/// </summary>
public struct ConnectedDevices() : IResource
{
    internal readonly Dictionary<DeviceId, DeviceKind> _devicesById = [];

    /// <summary>Every keyboard and mouse currently connected, keyed by SDL device id.</summary>
    public IReadOnlyDictionary<DeviceId, DeviceKind> DevicesById => _devicesById;
}
