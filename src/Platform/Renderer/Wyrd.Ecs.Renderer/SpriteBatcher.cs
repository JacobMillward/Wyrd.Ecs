namespace Wyrd.Ecs.Renderer;

/// <summary>One instanced draw call's worth of entities. Every entity here shares the same pipeline-selecting <see cref="Material"/>, so they can be drawn together, reading per-instance transform/tint/source-rect from the instance buffer.</summary>
internal readonly record struct SpriteBatch(Material Material, IReadOnlyList<Entity> Entities);

/// <summary>
/// Groups culling survivors by <see cref="Material"/>, preserving each group's original
/// relative order (query order), matching how the instance buffer is written in query order.
/// Reuses its grouping dictionary and each group's <see cref="List{T}"/> across calls instead
/// of allocating fresh ones every camera every frame. <c>_grouped</c>'s keys persist for the
/// process lifetime (bounded by the number of distinct <see cref="Material"/> values ever
/// seen, the same growth-only shape this codebase's type registries already use), only each
/// group's contents are cleared and rewritten per call.
/// </summary>
internal sealed class SpriteBatcher
{
    private readonly Dictionary<Material, List<Entity>> _grouped = new();
    private readonly List<Material> _order = [];
    private readonly List<SpriteBatch> _batches = [];

    public IReadOnlyList<SpriteBatch> Batch(IReadOnlyList<(Entity Entity, Material Material)> survivors)
    {
        _order.Clear();
        _batches.Clear();
        foreach (var entities in _grouped.Values) entities.Clear();

        foreach (var (entity, material) in survivors)
        {
            if (!_grouped.TryGetValue(material, out var entities))
            {
                entities = [];
                _grouped[material] = entities;
            }
            if (entities.Count == 0) _order.Add(material); // first entity this call for a previously-cleared (or brand new) group
            entities.Add(entity);
        }

        foreach (var material in _order)
            _batches.Add(new SpriteBatch(material, _grouped[material]));

        return _batches;
    }
}
