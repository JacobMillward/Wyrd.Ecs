namespace Wyrd.Ecs.Renderer;

/// <summary>
/// Batch key. Unlike <see cref="SpriteBatcher"/>'s <see cref="Material"/>-only key, mesh
/// batching also needs <see cref="Mesh"/> identity: two entities can share a
/// <see cref="Material"/> while pointing at different geometry, and those can't share one
/// instanced draw call (one bound vertex/index buffer per SDL_GPU draw).
/// </summary>
internal readonly record struct MeshBatchKey(Material Material, Handle<Mesh> Mesh);

/// <summary>One instanced draw call's worth of entities, all sharing both <see cref="Material"/> (pipeline/texture) and <see cref="Mesh"/> (geometry).</summary>
internal readonly record struct MeshBatch(Material Material, Handle<Mesh> Mesh, IReadOnlyList<Entity> Entities);

/// <summary>
/// Groups culling survivors by <see cref="MeshBatchKey"/>, preserving each group's original
/// relative order. Reuses its grouping dictionary and each group's <see cref="List{T}"/>
/// across calls instead of allocating fresh ones every camera every frame.
/// </summary>
internal sealed class MeshBatcher
{
    private readonly Dictionary<MeshBatchKey, List<Entity>> _grouped = new();
    private readonly List<MeshBatchKey> _order = [];
    private readonly List<MeshBatch> _batches = [];

    public IReadOnlyList<MeshBatch> Batch(IReadOnlyList<(Entity Entity, Material Material, Handle<Mesh> Mesh)> survivors)
    {
        _order.Clear();
        _batches.Clear();
        foreach (var entities in _grouped.Values) entities.Clear();

        foreach (var (entity, material, mesh) in survivors)
        {
            var key = new MeshBatchKey(material, mesh);
            if (!_grouped.TryGetValue(key, out var entities))
            {
                entities = [];
                _grouped[key] = entities;
            }
            if (entities.Count == 0) _order.Add(key);
            entities.Add(entity);
        }

        foreach (var key in _order)
            _batches.Add(new MeshBatch(key.Material, key.Mesh, _grouped[key]));

        return _batches;
    }
}
