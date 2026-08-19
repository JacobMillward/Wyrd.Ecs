namespace Wyrd.Ecs.Renderer;

/// <summary>
/// A 3D drawable, paired with a <see cref="Material"/> on the same entity. Unlike a sprite (an
/// implicit quad generated in the vertex shader), a mesh entity must name its own geometry:
/// <see cref="Mesh"/> isn't pipeline-selecting state (two different meshes can share one
/// <see cref="Material"/>), so it lives here, not on <see cref="Material"/>. <see cref="Tint"/>
/// lives here for the same reason: it's allowed to vary between entities sharing one
/// <see cref="Material"/>, and anything in the batch key that varies per-entity would fragment
/// every batch back down to one draw call per distinct value.
/// </summary>
public readonly record struct MeshRenderer(Handle<Mesh> Mesh, Color Tint) : IComponent;
