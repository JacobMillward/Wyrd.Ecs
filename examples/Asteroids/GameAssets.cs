using Wyrd.Ecs.Assets;
using Wyrd.Ecs.Audio;
using Wyrd.Ecs.Examples.Asteroids.Templates;

namespace Wyrd.Ecs.Examples.Asteroids;

public readonly record struct GameAssets(
    ShipTemplate ShipTemplate,
    BulletTemplate BulletTemplate,
    AsteroidTemplate AsteroidTemplate,
    Handle<Sound> LaserSound,
    Handle<Sound> ExplosionSound,
    Handle<Sound> EngineSound) : IResource;
