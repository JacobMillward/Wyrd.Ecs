namespace Wyrd.Ecs.Renderer;

/// <summary>
/// Whether a <see cref="Material"/>'s pipeline blends against the background or writes fully
/// opaque. Also selects depth-write behavior (see <see cref="RendererSystem"/>'s pipeline
/// cache): <see cref="Opaque"/> writes depth and tests it, draw order doesn't matter.
/// <see cref="Transparent"/> tests depth but doesn't write it, and must draw back-to-front for
/// correct compositing.
/// </summary>
public enum BlendMode
{
    /// <summary>Writes depth and tests it; draw order doesn't matter.</summary>
    Opaque,

    /// <summary>Tests depth but doesn't write it; must draw back-to-front for correct compositing.</summary>
    Transparent,
}
