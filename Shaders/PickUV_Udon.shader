Shader "Unlit/PickUV_Udon"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _UV; // xy = 0..1

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; };

            v2f vert (appdata v)
            { v2f o; o.pos = UnityObjectToClipPos(v.vertex); return o; }

            fixed4 frag (v2f i) : SV_Target
            { return tex2D(_MainTex, _UV.xy); }
            ENDCG
        }
    }
}
