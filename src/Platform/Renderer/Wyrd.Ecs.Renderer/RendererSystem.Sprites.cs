using System.Numerics;
using System.Runtime.InteropServices;
using SDL3;
using Wyrd.Ecs.Assets;

namespace Wyrd.Ecs.Renderer;

public sealed partial class RendererSystem
{
    // ArchetypeQuery/ArchetypeChunk (ArchetypeQuery.cs) is the lowest-level query primitive,
    // the only one giving entity-row-aligned Access<TAccessor>() at 3+ component arity without
    // generator setup. World.Query<T0,T1>'s hand-written ChunkAction overloads cap at arity 2
    // and never expose Entity, and the fluent Query<TShape>.ForEach() terminal (generator-
    // emitted) never exposes Entity either. Both ruled out since GetInterpolatedWorldTransform needs it.
    private static readonly ArchetypeQuery SpriteArchetypeQuery = ArchetypeQuery.Empty
        .Access<Ref<Transform>>().Access<Ref<Sprite>>().Access<Ref<Material>>();

    private readonly InstanceBuffer<SpriteInstanceData>?[] _instanceBuffersBySlot = new InstanceBuffer<SpriteInstanceData>?[FrameInFlightTracker.FramesInFlight];
    private readonly SpriteBatcher _spriteBatcher = new();
    private readonly List<(Entity Entity, WorldTransform Transform, Sprite Sprite, Material Material, BoundingSphere Bounds)> _spriteScratch = [];
    private readonly Dictionary<Entity, int> _spriteScratchIndex = new();
    private readonly List<(Entity Entity, Material Material)> _opaqueSpriteScratch = [];
    private readonly List<(Entity Entity, PipelineKey PipelineKey, Material Material, Handle<Mesh>? Mesh, float ViewSpaceDepth)> _transparentScratch = [];
    private readonly List<SpriteInstanceData> _spriteInstanceScratch = [];
    private readonly List<int> _spriteBatchInstanceBases = [];
    private readonly TransparentDrawableBatcher _transparentDrawableBatcher = new();

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct BatchUniforms(Vector2 TextureSizePixels, uint InstanceBase, uint Padding = 0);

    /// <summary>Resolves every active <c>(Transform, Sprite, Material)</c> entity's world transform and bounds once per frame, into <see cref="_spriteScratch"/>, so <see cref="DrawCamera"/> never repeats <see cref="World.GetInterpolatedWorldTransform"/> per camera. Called once from <see cref="DrawFrame"/>, before the per-camera loop.</summary>
    private void ResolveSprites(World world)
    {
        _spriteScratch.Clear();
        _spriteScratchIndex.Clear();
        foreach (var chunk in SpriteArchetypeQuery.Resolve(world))
        {
            var entities = chunk.Entities;
            var transforms = chunk.Access<Ref<Transform>>();
            var sprites = chunk.Access<Ref<Sprite>>();
            var materials = chunk.Access<Ref<Material>>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var entity = entities[i];
                var sprite = sprites[i];
                var material = materials[i];
                var worldTransform = world.GetInterpolatedWorldTransform(entity);
                var texture = ResolveTexture(material);
                var bounds = SpriteBounds.Compute(worldTransform, sprite, texture.PixelWidth, texture.PixelHeight);
                _spriteScratchIndex[entity] = _spriteScratch.Count;
                _spriteScratch.Add((entity, worldTransform, sprite, material, bounds));
            }
        }
    }

    /// <summary>Appends every entity in <paramref name="entities"/> (the whole list) already-resolved sprite instance data to <see cref="_spriteInstanceScratch"/>, in order. Shared by both phases: one instance buffer write per family covers batches from both.</summary>
    private void AppendSpriteInstances(IReadOnlyList<Entity> entities) => AppendSpriteInstances(entities, 0, entities.Count);

    /// <summary>Range overload for the transparent phase, which indexes a slice of <see cref="TransparentDrawableBatcher.Entities"/> rather than owning its own list per batch. See <see cref="TransparentBatch"/>'s doc comment.</summary>
    private void AppendSpriteInstances(IReadOnlyList<Entity> entities, int start, int count)
    {
        for (var i = start; i < start + count; i++)
        {
            var resolved = _spriteScratch[_spriteScratchIndex[entities[i]]];
            var sourceRect = resolved.Sprite.SourceRect is { } r ? new Vector4(r.X, r.Y, r.Width, r.Height) : Vector4.Zero;
            _spriteInstanceScratch.Add(new SpriteInstanceData(resolved.Transform.Position, resolved.Transform.Rotation, resolved.Transform.Scale, resolved.Sprite.Tint, sourceRect));
        }
    }

    /// <summary>Binds the pipeline/instance-buffer/sampler common to any sprite batch, then draws it. Independent per call (not "bind once per camera") so opaque and transparent phase batches, and within the transparent phase sprite and mesh batches interleaved in sorted order, can each rebind whatever they individually need. Shares <see cref="BindCommonBatchState"/> with <see cref="DrawMeshBatch"/>: the only sprite-specific piece here is <see cref="BatchUniforms"/>'s extra <c>TextureSizePixels</c> field and the non-indexed draw call.</summary>
    private void DrawSpriteBatch(IntPtr renderPass, IntPtr commandBuffer, IntPtr instanceBuffer, int instanceBase, Material material, int entityCount)
    {
        var texture = BindCommonBatchState(renderPass, instanceBuffer, material);

        var batchUniforms = new BatchUniforms(new Vector2(texture.PixelWidth, texture.PixelHeight), (uint)instanceBase);
        var batchUniformBytes = MemoryMarshal.AsBytes(new ReadOnlySpan<BatchUniforms>(in batchUniforms));
        SDL.PushGPUVertexUniformData(commandBuffer, 1, batchUniformBytes, (uint)batchUniformBytes.Length);

        SDL.DrawGPUPrimitives(renderPass, 4, (uint)entityCount, 0, 0); // firstInstance always 0. BatchUniforms.InstanceBase carries the real offset (see UnlitSprite.vert.hlsl)
    }

    private Texture ResolveTexture(Material material) =>
        material.Texture is { } handle && GetTextureLoadState(handle) == LoadState.Loaded
            ? _textureArena.TryGet(handle)!
            : PlaceholderTexture;
}
