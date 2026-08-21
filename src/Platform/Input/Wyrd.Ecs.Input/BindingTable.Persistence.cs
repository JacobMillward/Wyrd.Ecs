using SDL3;
using Wyrd.Ecs.Persistence;

namespace Wyrd.Ecs.Input;

public sealed partial class BindingTable<TAction>
{
    /// <summary>Same as <see cref="LoadOverrides(IPersistenceStore)"/>, wrapping <paramref name="path"/> in a <c>new FileStore(path)</c>.</summary>
    public void LoadOverrides(string path) => LoadOverrides(new FileStore(path));

    /// <summary>Loads a remap file from <paramref name="store"/>, replacing (not adding to) each action's bindings for every action the file actually contains. A missing checkpoint (no remap saved yet) is a no-op, leaving code defaults in place.</summary>
    public void LoadOverrides(IPersistenceStore store)
    {
        Stream stream;
        try
        {
            stream = store.OpenCheckpointRead();
        }
        catch (FileNotFoundException)
        {
            return;
        }

        using (stream)
        {
            var dto = System.Text.Json.JsonSerializer.Deserialize(stream, InputJsonContext.Default.BindingFileDto)
                ?? throw new InvalidOperationException("Remap file deserialized to null.");
            foreach (var profileDto in dto.Profiles)
            {
                var profile = new ProfileId(profileDto.Profile);
                foreach (var (actionName, keyNames) in profileDto.Keys)
                {
                    var action = Enum.Parse<TAction>(actionName);
                    Unbind(profile, action);
                    Bind(profile, action, [.. keyNames.Select(Enum.Parse<SDL.Scancode>)]);
                }
                foreach (var (actionName, buttonNames) in profileDto.MouseButtons)
                {
                    var action = Enum.Parse<TAction>(actionName);
                    Unbind(profile, action);
                    Bind(profile, action, [.. buttonNames.Select(Enum.Parse<MouseButton>)]);
                }
                foreach (var (actionName, axisNames) in profileDto.Axes)
                {
                    var action = Enum.Parse<TAction>(actionName);
                    Unbind(profile, action);
                    BindAxis2D(profile, action,
                        Enum.Parse<SDL.Scancode>(axisNames[0]), Enum.Parse<SDL.Scancode>(axisNames[1]),
                        Enum.Parse<SDL.Scancode>(axisNames[2]), Enum.Parse<SDL.Scancode>(axisNames[3]));
                }
            }
        }
    }

    /// <summary>Same as <see cref="SaveOverrides(IPersistenceStore)"/>, wrapping <paramref name="path"/> in a <c>new FileStore(path)</c>.</summary>
    public void SaveOverrides(string path) => SaveOverrides(new FileStore(path));

    /// <summary>Saves every current binding to <paramref name="store"/> as human-editable JSON, keyed by profile and action name. Device assignments (<see cref="AssignDevice"/>) are deliberately not saved: SDL device ids aren't stable across a relaunch.</summary>
    public void SaveOverrides(IPersistenceStore store)
    {
        var byProfile = BoundActions().Select(b => b.Profile).Distinct();
        var dto = new BindingFileDto();
        foreach (var profile in byProfile)
        {
            var profileDto = new ProfileBindingsDto { Profile = profile.Value };
            foreach (var (p, action, kind) in BoundActions().Where(b => b.Profile == profile))
            {
                var name = action.ToString();
                if (kind == Kind.Digital)
                {
                    var keys = KeysFor(p, action);
                    if (keys.Count > 0) profileDto.Keys[name] = [.. keys.Select(k => k.ToString())];
                    var buttons = MouseButtonsFor(p, action);
                    if (buttons.Count > 0) profileDto.MouseButtons[name] = [.. buttons.Select(b => b.ToString())];
                }
                else if (AxisFor(p, action) is { } axis)
                {
                    profileDto.Axes[name] = [axis.Up.ToString(), axis.Down.ToString(), axis.Left.ToString(), axis.Right.ToString()];
                }
            }
            dto.Profiles.Add(profileDto);
        }

        using var stream = store.OpenCheckpointWrite();
        System.Text.Json.JsonSerializer.Serialize(stream, dto, InputJsonContext.Default.BindingFileDto);
    }
}
