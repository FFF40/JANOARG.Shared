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
                // The split counts come from chart data. The Range attribute on the C# side only
                // constrains the inspector, so a hand-edited or deserialised value of 0 arrives
                // intact — and 1/0 turns the lerp below into NaN across the whole frame, at every
                // strength, because lerp multiplies the bad term by zero rather than skipping it.
                int splitsX = max(1, _SplitsX);
                int splitsY = max(1, _SplitsY);

                float2 uv = i.uv;
                float2 quad = trunc(uv * float2(splitsX, splitsY));

                float2 splitInv = float2(1.0 / splitsX, 1.0 / splitsY);
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
