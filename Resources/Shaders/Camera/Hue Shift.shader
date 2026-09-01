Shader "JANOARG/Effects/HueShift"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Strength ("Hue Shift", Range(0, 1)) = 0.5
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

            float3 hueShift(float3 color, float hueAdjust)
            {
                const float3 kRGBToYPrime = float3(0.299, 0.587, 0.114);
                const float3 kRGBToI      = float3(0.596, -0.275, -0.321);
                const float3 kRGBToQ      = float3(0.212, -0.523, 0.311);

                const float3 kYIQToR     = float3(1.0, 0.956, 0.621);
                const float3 kYIQToG     = float3(1.0, -0.272, -0.647);
                const float3 kYIQToB     = float3(1.0, -1.107, 1.704);

                float YPrime = dot(color, kRGBToYPrime);
                float I      = dot(color, kRGBToI);
                float Q      = dot(color, kRGBToQ);
                float chroma = sqrt(I * I + Q * Q);

                if (chroma < 1e-5)
                {
                    return color;
                }

                float hue = atan2(Q, I);
                hue += hueAdjust;

                Q = chroma * sin(hue);
                I = chroma * cos(hue);

                float3 yIQ = float3(YPrime, I, Q);

                return float3(dot(yIQ, kYIQToR), dot(yIQ, kYIQToG), dot(yIQ, kYIQToB));
            }

            fixed4 frag (v2f_img i) : SV_Target
            {
                fixed4 colour = tex2D(_MainTex, i.uv);

                float3 shiftedColour = hueShift(colour.rgb, radians(_Strength * 360.0));

                return fixed4(shiftedColour, colour.a);
            }
            ENDCG
        }
    }
}
