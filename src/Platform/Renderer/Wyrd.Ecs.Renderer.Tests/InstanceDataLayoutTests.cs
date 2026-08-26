using System.Runtime.InteropServices;

namespace Wyrd.Ecs.Renderer.Tests;

/// <summary>
/// Pins <see cref="SpriteInstanceData"/>/<see cref="MeshInstanceData"/>'s byte layout to what
/// <c>UnlitSprite.vert.hlsl</c>/<c>UnlitMesh.vert.hlsl</c> actually read: every <c>float4</c>
/// field aligned to a 16-byte boundary. No GPU needed - this is a C#-side layout fact, and a
/// mismatch here previously produced no error, just a silently wrong rotation and tint on
/// screen.
/// </summary>
public class InstanceDataLayoutTests
{
    [Fact]
    public void SpriteInstanceData_MatchesShaderStructuredBufferLayout()
    {
        Marshal.SizeOf<SpriteInstanceData>().Should().Be(80);
        Marshal.OffsetOf<SpriteInstanceData>(nameof(SpriteInstanceData.Position)).ToInt32().Should().Be(0);
        Marshal.OffsetOf<SpriteInstanceData>(nameof(SpriteInstanceData.Rotation)).ToInt32().Should().Be(16);
        Marshal.OffsetOf<SpriteInstanceData>(nameof(SpriteInstanceData.Scale)).ToInt32().Should().Be(32);
        Marshal.OffsetOf<SpriteInstanceData>(nameof(SpriteInstanceData.Tint)).ToInt32().Should().Be(48);
        Marshal.OffsetOf<SpriteInstanceData>(nameof(SpriteInstanceData.SourceRectPixels)).ToInt32().Should().Be(64);
    }

    [Fact]
    public void MeshInstanceData_MatchesShaderStructuredBufferLayout()
    {
        Marshal.SizeOf<MeshInstanceData>().Should().Be(64);
        Marshal.OffsetOf<MeshInstanceData>(nameof(MeshInstanceData.Position)).ToInt32().Should().Be(0);
        Marshal.OffsetOf<MeshInstanceData>(nameof(MeshInstanceData.Rotation)).ToInt32().Should().Be(16);
        Marshal.OffsetOf<MeshInstanceData>(nameof(MeshInstanceData.Scale)).ToInt32().Should().Be(32);
        Marshal.OffsetOf<MeshInstanceData>(nameof(MeshInstanceData.Tint)).ToInt32().Should().Be(48);
    }
}
