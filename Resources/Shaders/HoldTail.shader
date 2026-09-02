Shader "JANOARG/Hold Tail/Default"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Fade" }
        ZWrite Off
        Cull Off
        // Separate alpha blend, matching Hit.shader and Highlight.shader, so this composites
        // correctly into a transparent render target (the options panel preview camera clears
        // its RenderTexture to alpha 0). Against an opaque destination dstA is already 1 and
        // srcA + 1*(1-srcA) = 1, so gameplay rendering is unchanged.
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                // fogCoord only exists in the fog-enabled variants; multi_compile_fog
                // also generates FOG_OFF, where UNITY_FOG_COORDS declares nothing.
                float fade = 1;
                #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
                    fade = min(max(i.fogCoord.x, 0), 1);
                #endif
                col.a *= fade;
                return col;
            }
            ENDCG
        }
    }
}
