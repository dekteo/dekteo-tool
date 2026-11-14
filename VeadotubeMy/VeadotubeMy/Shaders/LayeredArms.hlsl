sampler2D image : register(s0);
float2 leftMove : register(c0);
float2 rightMove : register(c1);
float2 presses : register(c2);
float2 mode : register(c3);
float2 rightThumb : register(c4);
float2 leftThumb : register(c5);
float weight(float2 p,float2 c,float2 r){float2 d=(p-c)/r;float v=max(0,1-dot(d,d));return v*v;}
float4 main(float2 uv:TEXCOORD):COLOR {
 float2 p=uv*float2(1174,1339);
 float t=saturate((p.y-790)/300);t=t*t*(3-2*t);
 float side=saturate((p.x-550)/120);
 float2 move=lerp(leftMove,rightMove,side)*t;
 float2 a=lerp(float2(665,1056),float2(340,1190),mode.x);
 float2 b=lerp(float2(971,1067),float2(1000,1180),mode.x);
 float2 q=p-move;
 move.y+=presses.x*weight(q,a,float2(100,55))+presses.y*weight(q,b,float2(90,55));
 move+=rightThumb*weight(q,float2(678,970),float2(65,36));
 move+=leftThumb*weight(q,float2(945,977),float2(55,35));
 return tex2D(image,(p-move)/float2(1174,1339));
}
