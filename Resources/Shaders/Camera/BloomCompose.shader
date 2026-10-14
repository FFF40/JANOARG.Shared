Shader "JANOARG/Effects/BloomCompose"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        // Post-processing shaders should have these settings
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _AdditiveTex;
            float _Strength;

            // Ordered dither on the sum. Without it the accumulating bloom bands badly in the
            // dark areas of a chart, which is most of them.
            float dither(float2 uv)
            {
                return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453) - 0.5;
            }

            fixed4 frag (v2f_img i) : SV_Target
            {
                float3 scene = tex2D(_MainTex, i.uv).rgb;
                float3 add   = tex2D(_AdditiveTex, i.uv).rgb;
                float3 noise = dither(gl_FragCoord.xy) * _Strength * 5.0 / 255.0;

                return fixed4(scene + noise + add * _Strength, 1.0);
            }
            ENDCG
        }
    }
}