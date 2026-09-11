Shader "JANOARG/Effects/Noise"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Strength ("Noise Strength", Range(0, 1)) = 0.5
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

            float random(float2 st, float size)
            {
                st = floor(st * size) / size;

                if (st.x == 0.0) // to avoid a column of non-changing pixels
                    st = float2(0.4, st.y);

                return frac(sin(dot(st.xy, float2(_Time.y, 78.233))) * 43758.5453123);
            }

            fixed4 frag (v2f_img i) : SV_Target
            {
                fixed4 colour = tex2D(_MainTex, i.uv);

                float2 texSize = _MainTex_TexelSize.zw;
                float ratio = texSize.x / texSize.y;

                float rng = random(float2(i.uv.x * ratio, i.uv.y), texSize.y / 2.0);
                rng *= 0.75;
                float3 rngColor = float3(rng, rng, rng);

                return fixed4(lerp(colour.rgb, rngColor, _Strength), colour.a);
            }
            ENDCG
        }
    }
}
