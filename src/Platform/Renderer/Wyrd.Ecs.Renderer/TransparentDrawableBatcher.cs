using Wyrd.Ecs.Assets;

namespace Wyrd.Ecs.Renderer;

/// <summary>One instanced draw call's worth of entities for the transparent phase: same <see cref="PipelineKey"/> and <see cref="Material"/>, and for a mesh-family entry, the same <see cref="Mesh"/>. <see cref="Mesh"/> is null for a sprite-family entry. Entities are <see cref="TransparentDrawableBatcher.Entities"/>, sliced by <c>[EntityStart, EntityStart + EntityCount)</c>. See that type's doc comment for why this carries a range instead of its own list.</summary>
internal readonly record struct TransparentBatch(PipelineKey PipelineKey, Material Material, Handle<Mesh>? Mesh, int EntityStart, int EntityCount);

/// <summary>
/// Sorts transparent survivors from both families back-to-front by view-space depth (larger Z
/// means farther, per <see cref="CameraMath.GetViewMatrix"/>'s left-handed, forward-is-+Z
/// convention, so descending Z is drawn first), then batches only adjacent runs sharing the
/// same <see cref="PipelineKey"/>/<see cref="Material"/>/mesh. Unlike <see cref="SpriteBatcher"/>/
/// <see cref="MeshBatcher"/>, sort order takes priority over grouping: two same-key entries
/// separated by a different-key entry in depth order stay in two separate batches rather than
/// being merged out of order, since drawing them together would composite in the wrong order.
/// <see cref="Entities"/> is one flat list, sorted and reused across calls, with each
/// <see cref="TransparentBatch"/> indexing a range into it (the same flat-list-plus-offsets
/// shape <c>RendererSystem</c>'s <c>_spriteInstanceScratch</c>/<c>_spriteBatchInstanceBases</c>
/// already use), not one fresh <see cref="List{T}"/> per batch. A sorted run has no stable
/// per-key identity to size a persistent list by the way Sprite/MeshBatcher's grouping does, so
/// indexing into one reused list avoids an allocation per batch every frame instead.
/// </summary>
internal sealed class TransparentDrawableBatcher
{
    private readonly List<(Entity Entity, PipelineKey PipelineKey, Material Material, Handle<Mesh>? Mesh, float ViewSpaceDepth)> _sorted = [];
    private readonly List<Entity> _entities = [];
    private readonly List<TransparentBatch> _batches = [];

    /// <summary>The flat, sorted (back-to-front) entity list every <see cref="TransparentBatch"/> in the most recent <see cref="Batch"/> result indexes into.</summary>
    public IReadOnlyList<Entity> Entities => _entities;

    public IReadOnlyList<TransparentBatch> Batch(IReadOnlyList<(Entity Entity, PipelineKey PipelineKey, Material Material, Handle<Mesh>? Mesh, float ViewSpaceDepth)> survivors)
    {
        _sorted.Clear();
        _sorted.AddRange(survivors);
        _sorted.Sort(static (a, b) => b.ViewSpaceDepth.CompareTo(a.ViewSpaceDepth)); // descending: farthest first

        _entities.Clear();
        _batches.Clear();
        var runStart = 0;
        for (var i = 0; i < _sorted.Count; i++)
        {
            var current = _sorted[i];
            _entities.Add(current.Entity);

            var isLastInRun = i == _sorted.Count - 1
                || !_sorted[i + 1].PipelineKey.Equals(current.PipelineKey)
                || !_sorted[i + 1].Material.Equals(current.Material)
                || _sorted[i + 1].Mesh != current.Mesh;

            if (isLastInRun)
            {
                _batches.Add(new TransparentBatch(current.PipelineKey, current.Material, current.Mesh, runStart, _entities.Count - runStart));
                runStart = _entities.Count;
            }
        }

        return _batches;
    }
}
