namespace Wyrd.Ecs.Renderer;

/// <summary>
/// Spawns the entity hierarchy for a loaded multi-part model: one parent entity, one child per
/// <see cref="RendererSystem.ModelPart"/> carrying identity-relative <see cref="Transform"/>,
/// <see cref="MeshRenderer"/>, and <see cref="Material"/>. Reuses the existing <see cref="Parent"/>
/// relation and world-transform propagation unchanged, so a multi-material file becomes
/// multiple entities rather than a materials array on one <see cref="MeshRenderer"/>. Destroying
/// the parent already cascades to every child (<see cref="Parent"/> is <see cref="IDependent"/>),
/// and per-child bounds mean finer-grained culling than one merged mesh would give.
/// </summary>
public static class RendererSystemModelExtensions
{
    extension(RendererSystem renderer)
    {
        /// <summary>Spawns the parent/children hierarchy for an already-resolved <paramref name="parts"/> list (from <c>await renderer.LoadModel(path)</c>). Every part gets <see cref="ShaderKind.UnlitMesh"/> and <see cref="Color.White"/> tint; a consumer wanting per-part tint mutates the returned children's <see cref="MeshRenderer"/> afterward.</summary>
        public Entity SpawnModel(World world, IReadOnlyList<RendererSystem.ModelPart> parts, Transform transform)
        {
            var parentView = world.Commands.CreateEntity();
            parentView.AddTransform(transform);
            Entity parent = parentView;

            foreach (var part in parts)
            {
                var childView = world.Commands.CreateEntity();
                childView.AddTransform(Transform.Identity);
                childView.AddComponent(new MeshRenderer(part.Mesh, Color.White));
                childView.AddComponent(new Material(ShaderKind.UnlitMesh, part.Texture));
                childView.SetParent(parent);
            }

            return parent;
        }
    }
}
