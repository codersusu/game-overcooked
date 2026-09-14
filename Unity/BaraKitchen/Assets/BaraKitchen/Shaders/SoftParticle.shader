Shader "Bara Kitchen/Soft Particle" {
 Properties { _Color ("Tint", Color) = (1,1,1,1) }
 SubShader {
  Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
  Blend SrcAlpha OneMinusSrcAlpha
  Cull Off Lighting Off ZWrite Off
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   fixed4 _Color;
   struct appdata {float4 vertex:POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;};
   struct v2f {float4 pos:SV_POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;};
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.color=v.color*_Color;o.uv=v.uv;return o;}
   fixed4 frag(v2f i):SV_Target {
    float2 p=i.uv*2-1;
    float density=pow(saturate(1-dot(p,p)),1.5);
    return fixed4(i.color.rgb,i.color.a*density);
   }
   ENDCG
  }
 }
}
