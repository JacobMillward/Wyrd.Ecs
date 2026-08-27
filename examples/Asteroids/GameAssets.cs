using Wyrd.Ecs.Assets;
using Wyrd.Ecs.Audio;

namespace Wyrd.Ecs.Examples.Asteroids;

public readonly record struct GameAssets(
    EntityTemplate BulletTemplate,
    EntityTemplate AsteroidTemplate,
    Handle<Sound> LaserSound,
    Handle<Sound> ExplosionSound,
    Handle<Sound> EngineSound) : IResource;
