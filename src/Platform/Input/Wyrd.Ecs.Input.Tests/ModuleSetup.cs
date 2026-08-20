using System.Runtime.CompilerServices;

namespace Wyrd.Ecs.Input.Tests;

internal static class ModuleSetup
{
    [ModuleInitializer]
    public static void Init() => Environment.SetEnvironmentVariable("SDL_VIDEODRIVER", "dummy");
}
