using Wyrd.Ecs.Renderer;

namespace Wyrd.Ecs.Examples.Asteroids.Components;

public enum AsteroidSize : byte { Large, Medium, Small }

public struct Asteroid : IComponent
{
    public AsteroidSize Size;
}

public static class AsteroidSizeInfo
{
    public static float Scale(this AsteroidSize size) => size switch
    {
        AsteroidSize.Large => 1.4f,
        AsteroidSize.Medium => 0.8f,
        AsteroidSize.Small => 0.45f,
        _ => 1f,
    };

    public static float Radius(this AsteroidSize size) => size switch
    {
        AsteroidSize.Large => 45f,
        AsteroidSize.Medium => 26f,
        AsteroidSize.Small => 14f,
        _ => 1f,
    };

    public static int Points(this AsteroidSize size) => size switch
    {
        AsteroidSize.Large => 20,
        AsteroidSize.Medium => 50,
        AsteroidSize.Small => 100,
        _ => 0,
    };

    public static AsteroidSize? Smaller(this AsteroidSize size) => size switch
    {
        AsteroidSize.Large => AsteroidSize.Medium,
        AsteroidSize.Medium => AsteroidSize.Small,
        _ => null,
    };

    /// <summary>A per-size tint over the shared template's white default: cool for Large,
    /// neutral for Medium, hot for Small. Reads as an at-a-glance threat gradient.</summary>
    public static Color Tint(this AsteroidSize size) => size switch
    {
        AsteroidSize.Large => new Color(0.72f, 0.82f, 1f, 1f),
        AsteroidSize.Medium => Color.White,
        AsteroidSize.Small => new Color(1f, 0.55f, 0.45f, 1f),
        _ => Color.White,
    };
}
