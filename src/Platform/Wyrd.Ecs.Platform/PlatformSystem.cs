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
    }

    /// <inheritdoc/>
    protected override void Execute(World world, Time time)
    {
        _events.Clear();
        SDL.PumpEvents();
        while (SDL.PollEvent(out var ev))
        {
            _events.Add(ev);
        }
    }

    /// <inheritdoc/>
    protected override void OnDestroy()
    {
        SDL.DestroyWindow(Window);
        SDL.QuitSubSystem(SDL.InitFlags.Video);
    }
}
