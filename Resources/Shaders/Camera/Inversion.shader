Shader "JANOARG/Effects/Inversion"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Strength ("Inversion Strength", Range(0, 1)) = 1.0
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
                fixed4 colour = tex2D(_MainTex, i.uv);

                float3 inverted = 1.0 - colour.rgb;
                return fixed4(lerp(colour.rgb, inverted, _Strength), colour.a);
            }
            ENDCG
        }
    }
}
