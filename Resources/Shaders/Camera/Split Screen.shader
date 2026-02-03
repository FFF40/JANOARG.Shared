Shader "JANOARG/Effects/SplitScreen"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Strength ("Split Strength", Range(0, 1)) = 1.0
        _SplitsX ("Splits X", Int) = 2
        _SplitsY ("Splits Y", Int) = 2
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
            int _SplitsX;
            int _SplitsY;

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                float2 quad = trunc(uv * float2(_SplitsX, _SplitsY));

                float2 splitInv = float2(1.0 / _SplitsX, 1.0 / _SplitsY);
                float2 zoom = lerp(float2(1.0, 1.0), splitInv, float2(_Strength, _Strength));
                float2 offset = lerp(float2(0.0, 0.0), quad * splitInv, float2(_Strength, _Strength));

                float2 uvQuad = (uv - offset) / zoom;
                fixed4 colour = tex2D(_MainTex, uvQuad);

                return colour;
            }
            ENDCG
        }
    }
}
