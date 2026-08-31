using SDL3;

namespace Wyrd.Ecs.Platform;

/// <summary>
/// Owns SDL's Video subsystem lifecycle and the application window. <c>SDL_Init</c> runs in
/// the constructor, cleanup in <see cref="OnDestroy"/>, since those are the only real
/// create/destroy hooks <see cref="EcsSystem"/> has today. If a consumer never calls
/// <see cref="World.RemoveSystem(EcsSystem)"/> and just lets the process exit, cleanup never
/// runs; that's fine, SDL doesn't require <c>SDL_Quit</c> before process exit.
/// </summary>
public sealed class PlatformSystem : EcsSystem
{
    private readonly List<SDL.Event> _events = [];

    /// <summary>The native SDL window handle, for consumers that need direct SDL3-CS access.</summary>
    public IntPtr Window { get; }

    /// <summary>Every SDL event pumped this tick. Cleared and refilled once per <see cref="Execute"/>.</summary>
    public IReadOnlyList<SDL.Event> Events => _events;

    /// <summary>
    /// Calls <c>SDL_Init(Video)</c> and creates the window. Throws
    /// <see cref="InvalidOperationException"/> if either fails, wrapping <c>SDL_GetError()</c>.
    /// </summary>
    public PlatformSystem(World world, string title, int width, int height, SDL.WindowFlags flags = default)
    {
        if (!SDL.Init(SDL.InitFlags.Video))
            throw new InvalidOperationException($"SDL_Init(Video) failed: {SDL.GetError()}");

        Window = SDL.CreateWindow(title, width, height, flags);
        if (Window == IntPtr.Zero)
        {
            var error = SDL.GetError();
            SDL.QuitSubSystem(SDL.InitFlags.Video);
            throw new InvalidOperationException($"SDL_CreateWindow failed: {error}");
        }

        world.AddResource(new ConnectedDevices());
        ref var connected = ref world.GetResourceRef<ConnectedDevices>();
        foreach (var id in SDL.GetKeyboards(out _) ?? [])
            connected._devicesById[new DeviceId(id)] = DeviceKind.Keyboard;
        foreach (var id in SDL.GetMice(out _) ?? [])
            connected._devicesById[new DeviceId(id)] = DeviceKind.Mouse;
    }

    /// <inheritdoc/>
    protected override void Execute(World world, Time time)
    {
        _events.Clear();
        SDL.PumpEvents();
        ref var connected = ref world.GetResourceRef<ConnectedDevices>();
        while (SDL.PollEvent(out var ev))
        {
            _events.Add(ev);
            switch ((SDL.EventType)ev.Type)
            {
                case SDL.EventType.Quit:
                    world.RequestExit();
                    break;
                case SDL.EventType.KeyboardAdded:
                    world.Emit(new DeviceChange(new DeviceId(ev.KDevice.Which), DeviceKind.Keyboard, DeviceChangeKind.Connected));
                    connected._devicesById[new DeviceId(ev.KDevice.Which)] = DeviceKind.Keyboard;
                    break;
                case SDL.EventType.KeyboardRemoved:
                    world.Emit(new DeviceChange(new DeviceId(ev.KDevice.Which), DeviceKind.Keyboard, DeviceChangeKind.Disconnected));
                    connected._devicesById.Remove(new DeviceId(ev.KDevice.Which));
                    break;
                case SDL.EventType.MouseAdded:
                    world.Emit(new DeviceChange(new DeviceId(ev.MDevice.Which), DeviceKind.Mouse, DeviceChangeKind.Connected));
                    connected._devicesById[new DeviceId(ev.MDevice.Which)] = DeviceKind.Mouse;
                    break;
                case SDL.EventType.MouseRemoved:
                    world.Emit(new DeviceChange(new DeviceId(ev.MDevice.Which), DeviceKind.Mouse, DeviceChangeKind.Disconnected));
                    connected._devicesById.Remove(new DeviceId(ev.MDevice.Which));
                    break;
                case SDL.EventType.AudioDeviceAdded:
                    if (!ev.ADevice.Recording)
                    {
                        world.Emit(new DeviceChange(new DeviceId(ev.ADevice.Which), DeviceKind.AudioOutput, DeviceChangeKind.Connected));
                        connected._devicesById[new DeviceId(ev.ADevice.Which)] = DeviceKind.AudioOutput;
                    }
                    break;
                case SDL.EventType.AudioDeviceRemoved:
                    if (!ev.ADevice.Recording)
                    {
                        world.Emit(new DeviceChange(new DeviceId(ev.ADevice.Which), DeviceKind.AudioOutput, DeviceChangeKind.Disconnected));
                        connected._devicesById.Remove(new DeviceId(ev.ADevice.Which));
                    }
                    break;
            }
        }
    }

    /// <inheritdoc/>
    protected override void OnDestroy()
    {
        SDL.DestroyWindow(Window);
        SDL.QuitSubSystem(SDL.InitFlags.Video);
    }
}
