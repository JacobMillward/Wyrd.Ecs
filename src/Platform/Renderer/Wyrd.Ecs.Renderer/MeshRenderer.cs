namespace Wyrd.Ecs.Renderer;

/// <summary>
/// A 3D drawable, paired with a <see cref="Material"/> on the same entity. Unlike a sprite (an
/// implicit quad generated in the vertex shader), a mesh entity must name its own geometry:
/// <see cref="Mesh"/> isn't pipeline-selecting state (two different meshes can share one
/// <see cref="Material"/>), so it lives here, not on <see cref="Material"/>. <see cref="Tint"/>
/// lives here for the same reason: it's per-instance data, not batch-key data (see
/// <see cref="Material"/>'s doc comment).
/// </summary>
public readonly record struct MeshRenderer(Handle<Mesh> Mesh, Color Tint) : IComponent;
