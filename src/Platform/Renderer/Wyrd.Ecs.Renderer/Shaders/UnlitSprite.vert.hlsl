struct SpriteInstance
{
    float3 Position;
    float4 Rotation;    // quaternion (x, y, z, w)
    float3 Scale;
    float4 Tint;
    float4 SourceRectPixels; // (x, y, width, height); width/height == 0 means "whole texture"
};

StructuredBuffer<SpriteInstance> Instances : register(t0, space0);

// Split into two cbuffers deliberately: CameraBuffer is pushed once per camera (identical for
// every batch drawn under it); BatchBuffer is pushed once per batch (the only two values that
// actually vary per draw call). Keeping them separate lets the C# side avoid re-pushing the
// unchanging view/projection matrix on every batch.
//
// row_major is required here, not stylistic: HLSL's default cbuffer matrix packing is
// column-major, but System.Numerics.Matrix4x4 (what the C# side uploads byte-for-byte) is
// row-major with row-vector convention (v' = v * M, see Camera.GetViewMatrix). Without this
// annotation the shader would silently read a transposed matrix, no error, just wrong
// geometry. mul()'s argument order below (vector, matrix) matches that same convention.
cbuffer CameraBuffer : register(b0, space1)
{
    row_major float4x4 ViewProjection;
};

cbuffer BatchBuffer : register(b1, space1)
{
    float2 TextureSizePixels;
    uint InstanceBase;  // this batch's starting index into Instances, see note below on why
    uint _Padding;      // this is passed explicitly rather than relied on implicitly.
};

struct VertexOutput
{
    float4 Position : SV_Position;
    float2 UV : TEXCOORD0;
    float4 Tint : TEXCOORD1;
};

static const float2 QuadCorners[4] = { float2(-0.5, -0.5), float2(0.5, -0.5), float2(-0.5, 0.5), float2(0.5, 0.5) };
static const float2 QuadUVs[4] = { float2(0, 1), float2(1, 1), float2(0, 0), float2(1, 0) };

float3 RotateByQuaternion(float3 v, float4 q)
{
    float3 u = q.xyz;
    float s = q.w;
    return 2.0f * dot(u, v) * u + (s * s - dot(u, u)) * v + 2.0f * s * cross(u, v);
}

// instanceId is NOT offset by firstInstance on every backend: Vulkan's gl_InstanceIndex
// includes it per spec, but Direct3D's SV_InstanceID always starts at 0 per draw call
// regardless of the draw's StartInstanceLocation. Since this HLSL cross-compiles to both
// DXIL (Windows/D3D12) and SPIR-V (Linux/Vulkan) from the same source, relying on the
// implicit offset would silently read the wrong instance data on Windows only. Every draw
// call therefore always passes firstInstance=0, and BatchBuffer.InstanceBase carries the real
// offset explicitly instead, so behavior is identical on every backend.
VertexOutput main(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
{
    SpriteInstance instance = Instances[InstanceBase + instanceId];

    float2 sizePixels = instance.SourceRectPixels.zw;
    if (sizePixels.x == 0 && sizePixels.y == 0)
        sizePixels = TextureSizePixels;

    float2 localPosition = QuadCorners[vertexId] * sizePixels;
    float3 scaled = float3(localPosition * instance.Scale.xy, 0);
    float3 worldPosition = instance.Position + RotateByQuaternion(scaled, instance.Rotation);

    VertexOutput output;
    output.Position = mul(float4(worldPosition, 1.0), ViewProjection); // vector * matrix, row-vector convention, matches row_major above
    output.UV = QuadUVs[vertexId];
    output.Tint = instance.Tint;
    return output;
}
