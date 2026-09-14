Shader "Bara Kitchen/Soft Color" {
    Properties {
        _Color ("Colour", Color) = (1,1,1,1)
        _MainTex ("Colour texture", 2D) = "white" {}
        _ShadeFloor ("Minimum diffuse light", Range(0,1)) = 0.48
        _Wrap ("Soft light wrap", Range(0,1)) = 0.22
        _Gloss ("Soft specular", Range(0,1)) = 0.04
        _ShadowTint ("Shade tint", Color) = (0.96,0.97,1,1)
    }
    SubShader {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 150
        Pass {
            Name "BARA_FORWARD"
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color, _ShadowTint;
            half _ShadeFloor, _Wrap, _Gloss;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                SHADOW_COORDS(3)
            };
            v2f vert(appdata v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                TRANSFER_SHADOW(o);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target {
                fixed3 albedo = tex2D(_MainTex, i.uv).rgb * _Color.rgb;
                half diffuse = saturate((dot(normalize(i.worldNormal), normalize(UnityWorldSpaceLightDir(i.worldPos))) + _Wrap) / (1 + _Wrap));
                half shadow = SHADOW_ATTENUATION(i);
                // A bright diffuse floor preserves the painted palette while real
                // directional shadows and curved-surface shading keep the scene legible.
                half3 illumination = _ShadeFloor * _ShadowTint.rgb + (1 - _ShadeFloor) * diffuse * shadow * _LightColor0.rgb;
                half3 n=normalize(i.worldNormal);
                half3 view=normalize(UnityWorldSpaceViewDir(i.worldPos));
                half3 halfway=normalize(view+normalize(UnityWorldSpaceLightDir(i.worldPos)));
                half spec=pow(saturate(dot(n,halfway)),32)*_Gloss*shadow;
                half hemisphere=lerp(.92,1.07,saturate(n.y*.5+.5));
                return fixed4(albedo * illumination * hemisphere + spec*_LightColor0.rgb, 1);
            }
            ENDCG
        }
        UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
    }
    Fallback "Diffuse"
}
