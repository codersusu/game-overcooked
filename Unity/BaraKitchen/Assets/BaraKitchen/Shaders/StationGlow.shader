Shader "Bara Kitchen/Station Glow" {
 Properties { _Color("Glow",Color)=(1,.76,.25,1) _Service("Service",Float)=0 _Still("Reduce motion",Float)=0 }
 SubShader { Tags {"Queue"="Transparent+12" "RenderType"="Transparent"} Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
 Pass { CGPROGRAM
 #pragma target 3.0
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
 struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
 float4 _Color;float _Service,_Still;
 v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
 fixed4 frag(v2f i):SV_Target {float2 p=abs(i.uv*2-1);float radius=.22;float d=length(max(p-(.83-radius),0))-radius;float aa=max(fwidth(d),.007);float edge=(1-smoothstep(.014,.014+aa,abs(d)));float halo=exp(-abs(d)*23)*.19;float corner=smoothstep(.25,.52,min(p.x,p.y));float time=_Time.y*(1-_Still);float pulse=.85+.15*sin(time*3.4);float angle=atan2(i.uv.y-.5,i.uv.x-.5);float glint=pow(saturate(cos(angle-time*1.0)),22)*.45*(1-_Still);float fill=(1-smoothstep(-.04,0,d))*.025;float2 q=i.uv-float2(.5,.25);float chevron=1-smoothstep(.012,.027,abs(q.y-abs(q.x)*.7));chevron*=1-smoothstep(.12,.17,abs(q.x));chevron*=1-smoothstep(.10,.13,abs(q.y));float arrows=chevron*_Service*(.7+.3*sin(time*3));float alpha=(arrows+edge*(.55+.35*corner)+halo+fill+edge*glint)*pulse*_Color.a;return float4(lerp(_Color.rgb,float3(1,1,.8),glint),alpha);}
 ENDCG }
 }
}
