Shader "JANOARG/Effects/FishEye"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Strength ("Fish Eye Strength", Range(-1, 1)) = 0.5
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

            #ifndef PI
            #define PI 3.141593
            #endif

            float2 FishEyeUV(float2 uv)
            {
                float2 center = float2(0.5, 0.5);
                float corner = length(center);
                float2 d = uv - 0.5;
                float r = length(d);

                if (_Strength > 0.0)
                {
                    float fac = _Strength * PI * 0.5;
                    uv = float2(0.5, 0.5) + normalize(d) * tan(r * fac) * corner / tan(corner * fac);
                }
                else
                {
                    float fac = tan(_Strength) * PI * 2.0;
                    uv = float2(0.5, 0.5) + normalize(d) * atan(r * -fac) * 0.5 / atan(0.5 * -fac);
                }
                return uv;
            }

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 fisheyeUV = FishEyeUV(i.uv);
                fixed4 colour = tex2D(_MainTex, fisheyeUV);

                return colour;
            }
            ENDCG
        }
    }
}
