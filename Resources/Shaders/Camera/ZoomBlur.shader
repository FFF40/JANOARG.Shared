Shader "JANOARG/Effects/ZoomBlur"
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

                float sigma     = (_Sigma - 0.5) * 4.0;
                float scaleStep = sigma / float(SAMPLES);
                float jitter    = dither(uv);

                float4 colour       = 0;
                float  validSamples = 0;

                for (int s = 0; s < SAMPLES; ++s)
                {
                    float scale = 1.0 - (float(s) + jitter) * scaleStep;
                    float2 sampleUV = _Position + delta * scale;

                    // Taps that fall outside the frame contribute nothing rather than clamping in
                    // from the edge, and the total is divided by the count that actually landed,
                    // so the edges of the playfield do not smear.
                    float2 inside  = step(float2(0.0, 0.0), sampleUV) * step(sampleUV, float2(1.0, 1.0));
                    float  factor  = inside.x * inside.y;

                    colour       += tex2D(_MainTex, sampleUV) * factor;
                    validSamples += factor;
                }

                return validSamples > 0.0 ? colour / validSamples : tex2D(_MainTex, uv);
            }
            ENDCG
        }
    }
}