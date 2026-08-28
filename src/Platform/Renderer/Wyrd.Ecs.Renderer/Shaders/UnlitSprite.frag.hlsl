Texture2D SpriteTexture : register(t0, space2);
SamplerState SpriteSampler : register(s0, space2);

struct VertexOutput
{
    float4 Position : SV_Position;
    float2 UV : TEXCOORD0;
    float4 Tint : TEXCOORD1;
};

float4 main(VertexOutput input) : SV_Target0
{
    float4 color = SpriteTexture.Sample(SpriteSampler, input.UV) * input.Tint;
    color.rgb *= color.a; // premultiplied alpha, pairs with BuildBlendState's One/OneMinusSrcAlpha factors
    return color;
}
