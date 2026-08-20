namespace Wyrd.Ecs.Input;

/// <summary>The on-disk shape of a remap file: per profile, an action name maps to the physical input names bound to it.</summary>
public sealed class BindingFileDto
{
    /// <summary>Per-profile binding overrides.</summary>
    public List<ProfileBindingsDto> Profiles { get; set; } = [];
}

/// <summary>One profile's binding overrides.</summary>
public sealed class ProfileBindingsDto
{
    /// <summary>The profile these overrides apply to.</summary>
    public int Profile { get; set; }

    /// <summary>Action name to bound key names.</summary>
    public Dictionary<string, string[]> Keys { get; set; } = [];

    /// <summary>Action name to bound mouse button names.</summary>
    public Dictionary<string, string[]> MouseButtons { get; set; } = [];

    /// <summary>Action name to a 4-element [Up, Down, Left, Right] key-name array.</summary>
    public Dictionary<string, string[]> Axes { get; set; } = [];
}
