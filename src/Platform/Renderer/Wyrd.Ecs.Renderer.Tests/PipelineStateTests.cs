using SDL3;

namespace Wyrd.Ecs.Renderer.Tests;

public class PipelineStateTests
{
    [Fact]
    public void BuildBlendState_Opaque_HasBlendDisabled()
    {
        var state = RendererSystem.BuildBlendState(BlendMode.Opaque);

        state.EnableBlend.Should().BeFalse();
    }

    [Fact]
    public void BuildBlendState_Transparent_UsesPremultipliedAlphaFactors()
    {
        var state = RendererSystem.BuildBlendState(BlendMode.Transparent);

        state.EnableBlend.Should().BeTrue();
        state.SrcColorBlendFactor.Should().Be(SDL.GPUBlendFactor.One);
        state.DstColorBlendFactor.Should().Be(SDL.GPUBlendFactor.OneMinusSrcAlpha);
        state.SrcAlphaBlendFactor.Should().Be(SDL.GPUBlendFactor.One);
        state.DstAlphaBlendFactor.Should().Be(SDL.GPUBlendFactor.OneMinusSrcAlpha);
    }

    [Fact]
    public void BuildDepthStencilState_Opaque_TestsAndWritesDepth()
    {
        var state = RendererSystem.BuildDepthStencilState(BlendMode.Opaque);

        state.EnableDepthTest.Should().BeTrue();
        state.EnableDepthWrite.Should().BeTrue();
    }

    [Fact]
    public void BuildDepthStencilState_Transparent_TestsButDoesNotWriteDepth()
    {
        var state = RendererSystem.BuildDepthStencilState(BlendMode.Transparent);

        state.EnableDepthTest.Should().BeTrue();
        state.EnableDepthWrite.Should().BeFalse();
    }
}
