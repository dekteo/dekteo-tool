sampler2D image : register(s0);
sampler2D areas : register(s1);
sampler2D fingers : register(s2);
sampler2D device : register(s3);
float2 leftMove : register(c0);
float2 rightMove : register(c1);
float2 presses : register(c2);
float2 rightThumb : register(c3);
float2 leftThumb : register(c4);
float4 main(float2 uv:TEXCOORD):COLOR {
 float3 a=tex2D(areas,uv).rgb;
 float d=tex2D(device,uv).r;
 float2 arm=(leftMove*a.r+rightMove*a.g)/max(1,a.r+a.g);
 float2 move=lerp(arm,(leftMove+rightMove)*.5,d);
 float2 q=uv-move/float2(1174,1339);
 a=tex2D(areas,q).rgb;
 float3 f=tex2D(fingers,q).rgb;
 move.y+=presses.x*a.b+presses.y*f.r;
 move+=rightThumb*f.g+leftThumb*f.b;
 float2 sampleUV=uv-move/float2(1174,1339);
 return tex2D(image,sampleUV)*step(0,sampleUV.x)*step(0,sampleUV.y)*step(sampleUV.x,1)*step(sampleUV.y,1);
}
