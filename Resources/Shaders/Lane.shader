
Shader "JANOARG/Lane/Default"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _FadeStart ("Fade Start Distance", Float) = 10
        _FadeEnd ("Fade End Distance", Float) = 200
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Fade" "IgnoreProjectors"="True" }
        LOD 200
        Cull Off
        ZWrite Off

        CGPROGRAM
        // Unlit: the lane ribbon is a flat colour, so the previous Standard PBR + forward-shadow
        // path was pure per-pixel cost (a large one on mobile GPUs). LightingUnlit returns the
        // albedo directly, and the flags strip the ambient / vertex-light / additive-light /
        // meta variants that a flat transparent ribbon never uses.
        #pragma surface surf Unlit alpha:fade noforwardadd noambient novertexlights nometa
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        fixed4 _Color;
        float _FadeStart;
        float _FadeEnd;

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;

            float dist = distance(_WorldSpaceCameraPos, IN.worldPos);
            float fadeAlpha = 1 - saturate((dist - _FadeStart) / (_FadeEnd - _FadeStart));

            float alpha = c.a * fadeAlpha;

            // The far end of every lane fades to alpha 0 but still rasterises and blends. Discard
            // those fragments: for a ZWrite-off transparent pass this is pure fillrate/blend saving
            // right where the lane overdraw is worst.
            clip(alpha - 0.004);

            o.Albedo = c.rgb;
            o.Alpha = alpha;
        }

        half4 LightingUnlit(SurfaceOutput s, half3 lightDir, half atten)
        {
            return half4(s.Albedo, s.Alpha);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
