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
            foreach (var seatDto in dto.Seats)
            {
                foreach (var (actionName, keyNames) in seatDto.Keys)
                {
                    var action = Enum.Parse<TAction>(actionName);
                    Unbind(seatDto.Seat, action);
                    Bind(seatDto.Seat, action, [.. keyNames.Select(Enum.Parse<SDL.Scancode>)]);
                }
                foreach (var (actionName, buttonNames) in seatDto.MouseButtons)
                {
                    var action = Enum.Parse<TAction>(actionName);
                    Unbind(seatDto.Seat, action);
                    Bind(seatDto.Seat, action, [.. buttonNames.Select(Enum.Parse<MouseButton>)]);
                }
                foreach (var (actionName, axisNames) in seatDto.Axes)
                {
                    var action = Enum.Parse<TAction>(actionName);
                    Unbind(seatDto.Seat, action);
                    BindAxis2D(seatDto.Seat, action,
                        Enum.Parse<SDL.Scancode>(axisNames[0]), Enum.Parse<SDL.Scancode>(axisNames[1]),
                        Enum.Parse<SDL.Scancode>(axisNames[2]), Enum.Parse<SDL.Scancode>(axisNames[3]));
                }
            }
        }
    }

    /// <summary>Same as <see cref="SaveOverrides(IPersistenceStore)"/>, wrapping <paramref name="path"/> in a <c>new FileStore(path)</c>.</summary>
    public void SaveOverrides(string path) => SaveOverrides(new FileStore(path));

    /// <summary>Saves every current binding to <paramref name="store"/> as human-editable JSON, keyed by seat and action name. Device assignments (<see cref="AssignDevice"/>) are deliberately not saved - SDL device ids aren't stable across a relaunch.</summary>
    public void SaveOverrides(IPersistenceStore store)
    {
        var bySeat = BoundActions().Select(b => b.Seat).Distinct();
        var dto = new BindingFileDto();
        foreach (var seat in bySeat)
        {
            var seatDto = new SeatBindingsDto { Seat = seat };
            foreach (var (s, action, kind) in BoundActions().Where(b => b.Seat == seat))
            {
                var name = action.ToString();
                if (kind == Kind.Digital)
                {
                    var keys = KeysFor(s, action);
                    if (keys.Count > 0) seatDto.Keys[name] = [.. keys.Select(k => k.ToString())];
                    var buttons = MouseButtonsFor(s, action);
                    if (buttons.Count > 0) seatDto.MouseButtons[name] = [.. buttons.Select(b => b.ToString())];
                }
                else if (AxisFor(s, action) is { } axis)
                {
                    seatDto.Axes[name] = [axis.Up.ToString(), axis.Down.ToString(), axis.Left.ToString(), axis.Right.ToString()];
                }
            }
            dto.Seats.Add(seatDto);
        }

        using var stream = store.OpenCheckpointWrite();
        System.Text.Json.JsonSerializer.Serialize(stream, dto, InputJsonContext.Default.BindingFileDto);
    }
}
