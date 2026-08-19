using System.Numerics;

namespace Wyrd.Ecs.Renderer;

/// <summary>
/// Bounds derive from the texture's pixel dimensions (1 world unit = 1 pixel at unit
/// <see cref="Wyrd.Ecs.Transform.Scale"/>, combined with the entity's actual world scale and
/// the sprite's <see cref="Sprite.SourceRect"/>), not a separate authored size field.
/// </summary>
internal static class SpriteBounds
{
    public static BoundingSphere Compute(WorldTransform transform, Sprite sprite, int texturePixelWidth, int texturePixelHeight)
    {
        var (width, height) = sprite.SourceRect is { } rect ? (rect.Width, rect.Height) : (texturePixelWidth, texturePixelHeight);
        var halfDiagonal = new Vector2(width, height).Length() * 0.5f;
        var worldRadius = halfDiagonal * Math.Max(Math.Abs(transform.Scale.X), Math.Abs(transform.Scale.Y));
        return new BoundingSphere(transform.Position, worldRadius);
    }
}
