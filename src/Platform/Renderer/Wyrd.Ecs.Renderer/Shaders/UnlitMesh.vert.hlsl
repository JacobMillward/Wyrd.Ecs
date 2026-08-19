struct MeshInstance
{
    float3 Position;
    float4 Rotation;   // quaternion (x, y, z, w)
    float3 Scale;
    float4 Tint;
};

StructuredBuffer<MeshInstance> Instances : register(t0, space0);

// row_major / mul(vector, matrix) argument order: see UnlitSprite.vert.hlsl's identical note,
// same System.Numerics.Matrix4x4 row-major convention applies here.
cbuffer CameraBuffer : register(b0, space1)
{
    row_major float4x4 ViewProjection;
};

cbuffer BatchBuffer : register(b1, space1)
{
    uint InstanceBase;
    uint _Padding;
};

// Real vertex buffer, unlike UnlitSprite's SV_VertexID-generated quad: one MeshVertex per
// input, TEXCOORD0/1/2 matching MeshVertex's Position/Normal/UV field order exactly.
struct VertexInput
{
    float3 Position : TEXCOORD0;
    float3 Normal : TEXCOORD1;
    float2 UV : TEXCOORD2;
};

struct VertexOutput
{
    float4 Position : SV_Position;
    float2 UV : TEXCOORD0;
    float4 Tint : TEXCOORD1;
};

float3 RotateByQuaternion(float3 v, float4 q)
{
    float3 u = q.xyz;
    float s = q.w;
    return 2.0f * dot(u, v) * u + (s * s - dot(u, u)) * v + 2.0f * s * cross(u, v);
}

// Same firstInstance/SV_InstanceID portability note as UnlitSprite.vert.hlsl: always drawn with
// firstInstance=0, BatchBuffer.InstanceBase carries the real offset explicitly.
VertexOutput main(VertexInput input, uint instanceId : SV_InstanceID)
{
    MeshInstance instance = Instances[InstanceBase + instanceId];

    float3 scaled = input.Position * instance.Scale;
    float3 worldPosition = instance.Position + RotateByQuaternion(scaled, instance.Rotation);

    VertexOutput output;
    output.Position = mul(float4(worldPosition, 1.0), ViewProjection);
    output.UV = input.UV;
    output.Tint = instance.Tint;
    return output;
}
