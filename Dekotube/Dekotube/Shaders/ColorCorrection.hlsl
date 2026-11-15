sampler2D inputImage : register(s0);
sampler2D protection : register(s1);
float darkness : register(c9);
float2 shineStart : register(c10);
float2 shineEnd : register(c11);
float brightness : register(c0);
float contrast : register(c1);
float gammaValue : register(c2);
float2 hueRotation : register(c3);
float4 lightLeft : register(c4);
float4 lightRight : register(c5);
float lightStrength : register(c6);
float glintPosition : register(c7);
float glintStrength : register(c8);
float4 main(float2 uv : TEXCOORD) : COLOR
{
    float4 pixel = tex2D(inputImage, uv);
    float3 rgb = pixel.rgb / max(pixel.a, 0.00001);
    // Hue rotation around the neutral RGB axis keeps greys neutral.
    float c = hueRotation.x, s = hueRotation.y;
    float3 axis = float3(0.577350269,0.577350269,0.577350269);
    rgb = rgb*c + cross(axis,rgb)*s + axis*dot(axis,rgb)*(1-c);
    rgb = saturate((rgb-0.5)*contrast+0.5+brightness);
    rgb = pow(rgb, 1.0/gammaValue);
    float3 light = lerp(lightLeft.rgb, lightRight.rgb, saturate(uv.x));
    float influence = 1-tex2D(protection,uv).r;
    float luminance = dot(light,float3(.2126,.7152,.0722));
    rgb *= 1-darkness*(1-saturate(luminance*2))*influence;
    rgb = saturate(rgb + light * lightStrength * influence * (1-rgb));
    float glint = saturate(1-abs(uv.x+uv.y*.25-glintPosition)/.10);
    float inside = step(shineStart.x,uv.x)*step(shineStart.y,uv.y)*step(uv.x,shineEnd.x)*step(uv.y,shineEnd.y);
    rgb = saturate(rgb+glint*glint*glintStrength*.65*inside);
    return float4(rgb*pixel.a, pixel.a);
}
