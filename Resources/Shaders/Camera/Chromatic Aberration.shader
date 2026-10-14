Shader "JANOARG/Effects/ChromaticAberration"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Strength ("Chromatic Aberration Strength", Range(0, 10)) = 1.0
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

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                fixed4 colour = tex2D(_MainTex, uv);

                float2 offset = float2(0.001, 0.0) * _Strength;
                colour.r = tex2D(_MainTex, uv + offset).r;
                colour.b = tex2D(_MainTex, uv - offset).b;

                return colour;
            }
            ENDCG
        }
    }
}
