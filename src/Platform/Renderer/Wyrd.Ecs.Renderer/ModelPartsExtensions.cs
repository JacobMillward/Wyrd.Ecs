namespace Wyrd.Ecs.Renderer;

/// <summary>
/// Turns an already-resolved <c>await LoadModel(path)</c> result into a reusable
/// <see cref="EntityTemplate"/>: one child per <see cref="RendererSystem.ModelPart"/>, since a
/// multi-material file becomes multiple mesh assets from one path, and this ECS represents one
/// mesh plus one material per entity, not a materials array on one component.
/// </summary>
public static class ModelPartsExtensions
{
    extension(IReadOnlyList<RendererSystem.ModelPart> parts)
    {
        /// <summary>
        /// Builds the template: one child per part, each with <see cref="Transform.Identity"/>
        /// (paired with <see cref="PreviousTransform"/> via <see cref="EntityTemplate.AddTransform"/>),
        /// <see cref="MeshRenderer"/>, and <see cref="Material"/>(<see cref="ShaderKind.UnlitMesh"/>,
        /// tint <see cref="Color.White"/>). The root carries the same paired
        /// <see cref="Transform.Identity"/>/<see cref="PreviousTransform"/> and nothing else,
        /// free for the caller to extend (add components, tags, more children, or reposition
        /// via <c>CreateEntity(template).AddTransform(...)</c>) before the first
        /// <c>CreateEntity(template)</c> call, an <see cref="EntityTemplate"/> freezes on first
        /// instantiation like any other. Per-part tint is a post-creation mutation on the
        /// spawned children's <see cref="MeshRenderer"/>, not a template parameter.
        /// </summary>
        public EntityTemplate ToEntityTemplate()
        {
            var root = new EntityTemplate().AddTransform(Transform.Identity);
            foreach (var part in parts)
            {
                var child = new EntityTemplate()
                    .AddTransform(Transform.Identity)
                    .AddComponent(new MeshRenderer(part.Mesh, Color.White))
                    .AddComponent(new Material(ShaderKind.UnlitMesh, part.Texture));
                root.AddChild(child);
            }
            return root;
        }
    }
}
