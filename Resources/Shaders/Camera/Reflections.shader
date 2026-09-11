Shader "JANOARG/Effects/Reflections"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Strength ("Reflection Strength", Range(0, 1)) = 0.5
        _Scale ("Scale", Range(0, 1)) = 0.1
    }
    SubShader
    {
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
            float _Scale;

            static const float threshold = 0.01;
            static const float maxSamples = 10.0;

            fixed4 frag (v2f_img i) : SV_Target
            {
                // How many times the effect will iterate till it is barely visible, caps at 10
                float samples = maxSamples;
                if (_Strength != 1.0)
                    samples = min(floor(log(threshold) / log(_Strength)) + 1.0, maxSamples);

                float2 toCenter = i.uv - float2(0.5, 0.5);

                // Init parameters
                float3 color = float3(0.0, 0.0, 0.0);
                float2 scaleFac = _Scale + float2(1.0, 1.0);
                float2 scale = float2(1.0, 1.0);
                float strength = 1.0;

                for (float j = 0.0; j < maxSamples; j++)
                {
                    if (j >= samples) break;

                    float2 sampleUV = toCenter / scale + float2(0.5, 0.5);
                    fixed4 sampled = tex2D(_MainTex, sampleUV);
                    color += strength * sampled.w * sampled.xyz;
                    scale *= scaleFac;
                    strength *= _Strength;
                }

                return fixed4(color, 1.0);
            }
            ENDCG
        }
    }
}
