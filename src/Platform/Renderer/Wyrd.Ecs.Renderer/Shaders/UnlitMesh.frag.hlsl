Texture2D MeshTexture : register(t0, space2);
SamplerState MeshSampler : register(s0, space2);

struct VertexOutput
{
    float4 Position : SV_Position;
    float2 UV : TEXCOORD0;
    float4 Tint : TEXCOORD1;
};

float4 main(VertexOutput input) : SV_Target0
{
    return MeshTexture.Sample(MeshSampler, input.UV) * input.Tint;
}
