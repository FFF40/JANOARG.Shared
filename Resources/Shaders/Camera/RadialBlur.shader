Shader "JANOARG/Effects/RadialBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Position ("Blur Centre", Vector) = (0.5, 0.5, 0, 0)
        _Sigma ("Sigma", Float) = 0.5
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
            float4 _MainTex_TexelSize;
            float2 _Position;
            float _Sigma;

            #define SAMPLES 20

            float dither(float2 uv)
            {
                return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453) - 0.5;
            }

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 uv    = i.uv;
                float2 delta = uv - _Position;

                // Correct for the frame not being square, or the sweep comes out as an ellipse.
                // _MainTex_TexelSize is (w, h, 1/w, 1/h), so the ratio lives in .x/.y.
                float aspect    = _MainTex_TexelSize.x / _MainTex_TexelSize.y;
                float invAspect = 1.0 / aspect;
                delta.x *= aspect;

                float angleStep = ((_Sigma - 0.5) * 4.0) / float(SAMPLES);

                // Jitter the starting angle per pixel so the bands of a fixed-step sweep turn into
                // noise instead of visible spokes.
                float jitter    = dither(uv) * angleStep;
                float sinJitter = sin(jitter);
                float cosJitter = cos(jitter);

                float2 rotated = float2(
                    cosJitter * delta.x - sinJitter * delta.y,
                    sinJitter * delta.x + cosJitter * delta.y
                );

                float sinStep = sin(angleStep);
                float cosStep = cos(angleStep);

                float4 colour       = 0;
                float  validSamples = 0;

                for (int s = 0; s < SAMPLES; ++s)
                {
                    float2 sampleUV = _Position + float2(rotated.x * invAspect, rotated.y);

                    float2 inside = step(float2(0.0, 0.0), sampleUV) * step(sampleUV, float2(1.0, 1.0));
                    float  factor = inside.x * inside.y;

                    colour       += tex2D(_MainTex, sampleUV) * factor;
                    validSamples += factor;

                    rotated = float2(
                        cosStep * rotated.x - sinStep * rotated.y,
                        sinStep * rotated.x + cosStep * rotated.y
                    );
                }

                return validSamples > 0.0 ? colour / validSamples : tex2D(_MainTex, uv);
            }
            ENDCG
        }
    }
}