Shader "JANOARG/Effects/Mosaic"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Strength ("Mosaic Strength", Range(0, 1)) = 0.5
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
            float _Strength;

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 texSize = _MainTex_TexelSize.zw;

                float pixelSizeFactor = lerp(1.0, min(texSize.x, texSize.y), 1.0 - _Strength);
                float2 pixelSize = float2(pixelSizeFactor, pixelSizeFactor * (texSize.y / texSize.x));
                float2 pixelatedUV = (floor(i.uv * pixelSize) + 0.5) / pixelSize;

                return tex2Dlod(_MainTex, float4(pixelatedUV, 0, 0));
            }
            ENDCG
        }
    }
}
