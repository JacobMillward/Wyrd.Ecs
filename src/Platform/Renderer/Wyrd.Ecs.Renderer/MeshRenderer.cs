namespace Wyrd.Ecs.Renderer;

/// <summary>
/// A 3D drawable, paired with a <see cref="Material"/> on the same entity, mirroring
/// <see cref="Sprite"/>'s role. Unlike a sprite (an implicit quad generated in the vertex
/// shader), a mesh entity must name its own geometry: <see cref="Mesh"/> isn't
/// pipeline-selecting state (two different meshes can share one <see cref="Material"/>), so it
/// lives here, not on <see cref="Material"/>. <see cref="Tint"/> is the same per-instance
/// concept as <see cref="Sprite.Tint"/>, for the same reason (see <see cref="Material"/>'s doc
/// comment).
/// </summary>
public readonly record struct MeshRenderer(Handle<Mesh> Mesh, Color Tint) : IComponent;
