namespace Wyrd.Ecs.Renderer;

/// <summary>
/// Open, extensible pipeline identifier — not a closed enum, so a future user-authored-shader
/// spec can add new kinds without redesigning <see cref="Material"/>. Equality is by
/// <see cref="Name"/>, matching how the batching key ("2D batching and instancing" in the
/// spec) needs two <see cref="Material"/>s with the same shader to compare equal.
/// </summary>
public readonly record struct ShaderKind(string Name)
{
    /// <summary>The v1 2D sprite pipeline — unlit, textured, tinted.</summary>
    public static readonly ShaderKind UnlitSprite = new(nameof(UnlitSprite));
}
