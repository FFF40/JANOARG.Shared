Shader "JANOARG/Effects/ChromaticAberration"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Strength ("Chromatic Aberration Strength", Range(0, 1)) = 0.5
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

                // 0.004 of screen width is roughly 8px at 1080p, so the full 0-1 slider is
                // usable. The split is diagonal; a purely horizontal one reads as a colour
                // fringe rather than as lens dispersion.
                float2 offset = float2(0.004, 0.002) * _Strength;

                // Clamp rather than letting the default Repeat wrap opposite screen edges
                // into each other, which shows up as a bright seam down the frame.
                colour.r = tex2D(_MainTex, clamp(uv + offset, 0.0, 1.0)).r;
                colour.b = tex2D(_MainTex, clamp(uv - offset, 0.0, 1.0)).b;

                return colour;
            }
            ENDCG
        }
    }
}
