Shader "JANOARG/Highlight/Default"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        // Alpha blended, not additive: charts run on both dark and light backgrounds
        // (13 of 38 have a background brighter than 0.5 luminance), and adding light
        // on a near-white field just clips to white and destroys the note's colour.
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
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

            // The fog coordinate is the project's spawn fade: notes ramp in over it as
            // they enter the far end of the lane. The highlight has to use the same ramp
            // or it pops in while the note it belongs to fades. Matched to HoldTail's
            // curve rather than Hit's 1.2x, so highlight and note fade in together.
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;

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
