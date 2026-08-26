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
}
