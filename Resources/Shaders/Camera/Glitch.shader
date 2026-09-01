Shader "JANOARG/Effects/Glitch"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _StrengthX ("Glitch Strength X", Range(0, 1)) = 0.1
        _StrengthY ("Glitch Strength Y", Range(0, 1)) = 0.1
        _BlockSize ("Block Size", Range(0, 1)) = 0.5
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
            float _StrengthX;
            float _StrengthY;
            float _BlockSize;

            float random(float2 st, float seed)
            {
                return frac(sin(dot(st.xy, float2(12.9898, 78.233) + seed)) * 43758.5453123);
            }

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 texSize = _MainTex_TexelSize.zw;

                float blockSizeInPixels = lerp(1.0, min(texSize.x, texSize.y), _BlockSize);

                float2 blockUV = floor(i.uv * blockSizeInPixels) / blockSizeInPixels;

                float randomShiftX = (random(blockUV, _Time.y) - 0.5) * _StrengthX;
                float randomShiftY = (random(blockUV + float2(5.0, 5.0), _Time.y) - 0.5) * _StrengthY;

                float2 fixedUV = i.uv;
                fixedUV.x += randomShiftX;
                fixedUV.y += randomShiftY;

                fixed4 pixelColor = tex2Dlod(_MainTex, float4(fixedUV, 0, 0));

                return pixelColor;
            }
            ENDCG
        }
    }
}
