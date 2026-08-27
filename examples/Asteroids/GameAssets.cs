namespace Wyrd.Ecs.Examples.Asteroids;

public readonly record struct GameAssets(EntityTemplate BulletTemplate, EntityTemplate AsteroidTemplate) : IResource;
