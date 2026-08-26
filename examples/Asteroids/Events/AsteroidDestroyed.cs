using System.Numerics;
using Wyrd.Ecs.Examples.Asteroids.Components;

namespace Wyrd.Ecs.Examples.Asteroids.Events;

public struct AsteroidDestroyed(AsteroidSize size, Vector3 position) : IEvent
{
    public AsteroidSize Size = size;
    public Vector3 Position = position;
}
