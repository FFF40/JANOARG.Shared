Shader "JANOARG/Effects/Bloom"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Radius ("Blur Radius", Range(1, 100)) = 50
        _Sigma ("Blur Sigma", Range(0.1, 100)) = 50
        _BlurDirection ("Blur Direction", Vector) = (1, 0, 0, 0)
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
            float4 _MainTex_TexelSize;
            int _Radius;
            float _Sigma;
            float2 _BlurDirection;

            #define INV_SQRT_2PI 0.39894

            float computeGauss(float x, float sigma)
            {
                return INV_SQRT_2PI * exp(-0.5 * x * x / (sigma * sigma)) / sigma;
            }

            float4 blur(int radius, float2 direction, float2 texCoord, float2 texSize, float sigma)
            {
                float factor = computeGauss(0.0, sigma);
                float4 sum = tex2D(_MainTex, texCoord) * factor;

                float totalFactor = factor;

                for (int i = 2; i <= 200; i += 2)
                {
                    float x = float(i) - 0.5;
                    factor = computeGauss(x, sigma) * 2.0;
                    totalFactor += 2.0 * factor;

                    sum += tex2D(_MainTex, texCoord + direction * x / texSize) * factor;
                    sum += tex2D(_MainTex, texCoord - direction * x / texSize) * factor;

                    if (i >= radius) break;
                }

                return sum / totalFactor;
            }

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 texSize = _MainTex_TexelSize.zw; // .zw contains width and height

                float4 blurColour = blur(_Radius, _BlurDirection, i.uv, texSize, _Sigma);

                return blurColour;
            }
            ENDCG
        }
    }
}
