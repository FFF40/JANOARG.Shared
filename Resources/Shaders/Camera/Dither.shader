Shader "JANOARG/Effects/Dither"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Strength ("Dither Strength", Float) = 3
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
            float _Strength;

            // _ScreenParams.xy times uv, rather than gl_FragCoord: Unity cross-compiles CG to
            // GLSL on this platform and gl_FragCoord does not exist there.
            float dither(float2 uv)
            {
                return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453) - 0.5;
            }

            fixed4 frag (v2f_img i) : SV_Target
            {
                fixed4 colour = tex2D(_MainTex, i.uv);
                float3 noise  = dither(i.uv * _ScreenParams.xy) * _Strength / 255.0;

                return fixed4(colour.rgb + noise, colour.a);
            }
            ENDCG
        }
    }
}