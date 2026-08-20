namespace Wyrd.Ecs.Input;

/// <summary>The on-disk shape of a remap file: per seat, an action name maps to the physical input names bound to it.</summary>
public sealed class BindingFileDto
{
    /// <summary>Per-seat binding overrides.</summary>
    public List<SeatBindingsDto> Seats { get; set; } = [];
}

/// <summary>One seat's binding overrides.</summary>
public sealed class SeatBindingsDto
{
    /// <summary>The seat these overrides apply to.</summary>
    public int Seat { get; set; }

    /// <summary>Action name to bound key names.</summary>
    public Dictionary<string, string[]> Keys { get; set; } = [];

    /// <summary>Action name to bound mouse button names.</summary>
    public Dictionary<string, string[]> MouseButtons { get; set; } = [];

    /// <summary>Action name to a 4-element [Up, Down, Left, Right] key-name array.</summary>
    public Dictionary<string, string[]> Axes { get; set; } = [];
}
