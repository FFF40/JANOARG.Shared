
Shader "JANOARG/Styles/Default - Lane"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
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
        #pragma surface surf Standard fullforwardshadows alpha:fade
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        float _FadeStart;
        float _FadeEnd;

        UNITY_INSTANCING_BUFFER_START(Props)
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            
            float dist = distance(_WorldSpaceCameraPos, IN.worldPos);
            float fadeAlpha = 1 - saturate((dist - _FadeStart) / (_FadeEnd - _FadeStart));
            
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a * fadeAlpha;
        }
        ENDCG
    }
    FallBack "Diffuse"
}