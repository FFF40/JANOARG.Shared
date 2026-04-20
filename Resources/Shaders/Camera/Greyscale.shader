Shader "JANOARG/Effects/Greyscale"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Strength ("Greyscale Strength", Range(0, 1)) = 1.0
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

                float grey = dot(colour.rgb, float3(0.299, 0.587, 0.114));
                float3 greyColour = float3(grey, grey, grey);

                return lerp(colour, fixed4(greyColour, colour.a), _Strength);
            }
            ENDCG
        }
    }
}
