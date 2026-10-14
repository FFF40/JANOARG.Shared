Shader "JANOARG/Effects/BloomCompose"
{
    Properties
    {
        // As in Bloom.shader: anything the C# side writes has to be declared here to be settable.
        _MainTex ("Texture", 2D) = "white" {}
        _AdditiveTex ("Bloom", 2D) = "black" {}
        _Strength ("Strength", Float) = 0.5
        _Threshold ("Threshold", Range(0, 1)) = 0.65
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
            float _Threshold;

            // Ordered dither on the sum. Without it the accumulating bloom bands badly in the
            // dark areas of a chart, which is most of them.
            //
            // _ScreenParams.xy times uv, rather than gl_FragCoord: Unity cross-compiles CG to
            // GLSL on this platform and gl_FragCoord does not exist there. The shader fails to
            // compile outright and Unity draws the whole pass with its magenta error shader.
            float dither(float2 uv)
            {
                return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453) - 0.5;
            }

            fixed4 frag (v2f_img i) : SV_Target
            {
                float3 scene = tex2D(_MainTex, i.uv).rgb;
                float3 add   = tex2D(_AdditiveTex, i.uv).rgb;
                float3 noise = dither(i.uv * _ScreenParams.xy) * _Strength * 5.0 / 255.0;

                // fluXis adds the whole blurred frame back ungated. That reads as a glow because
                // a fluXis playfield is dark and the notes are the bright things on it. Here the
                // palette background is frequently the brightest surface in frame, so an ungated
                // add roughly doubles the entire image and washes the chart out to a flat field.
                // Gating on luminance puts the glow back on whatever should actually glow.
                // Zero is exact fluXis behaviour.
                float lum = dot(add, float3(0.299, 0.587, 0.114));
                add *= saturate((lum - _Threshold) / max(1e-4, 1.0 - _Threshold));

                return fixed4(scene + noise + add * _Strength, 1.0);
            }
            ENDCG
        }
    }
}