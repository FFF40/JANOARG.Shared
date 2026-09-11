Shader "JANOARG/Effects/Retro"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Strength ("Retro Strength", Range(0, 1)) = 0.5
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

            // https://www.shadertoy.com/view/WsVSzV

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 uv = i.uv;

                float2 dc = abs(float2(0.5, 0.5) - uv);
                dc *= dc;

                uv.x -= 0.5;
                uv.x *= 1.0 + (dc.y * (0.3 * _Strength));
                uv.x += 0.5;

                uv.y -= 0.5;
                uv.y *= 1.0 + (dc.x * (0.4 * _Strength));
                uv.y += 0.5;

                if (uv.y > 1.0 || uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0)
                {
                    return fixed4(0.0, 0.0, 0.0, 1.0);
                }
                else
                {
                    float2 screenPos = i.uv * _MainTex_TexelSize.zw;
                    float apply = abs(sin(screenPos.y) * 0.5 * _Strength);
                    return fixed4(lerp(tex2D(_MainTex, uv).rgb, float3(0.0, 0.0, 0.0), apply), 1.0);
                }
            }
            ENDCG
        }
    }
}
