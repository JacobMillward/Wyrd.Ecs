namespace Wyrd.Ecs.Renderer;

/// <summary>
/// A 2D drawable, paired with a <see cref="Material"/> on the same entity. Carries the
/// per-instance data allowed to vary within a batch: <see cref="SourceRect"/> (null = whole
/// texture; a sub-region for spritesheets, and later, atlas packing, see the spec's "2D
/// batching and instancing") and <see cref="Tint"/> (deliberately not on <see cref="Material"/>,
/// see that type's doc comment).
/// </summary>
public readonly record struct Sprite(Rect? SourceRect, Color Tint) : IComponent;
