namespace Wyrd.Ecs.Renderer;

/// <summary>Pixel-space sub-rectangle into a texture (spritesheet frame, atlas region). Not normalized UV — converted at draw time against the texture's real pixel dimensions.</summary>
public readonly record struct Rect(float X, float Y, float Width, float Height);
