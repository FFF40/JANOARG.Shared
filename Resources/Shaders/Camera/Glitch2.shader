Shader "JANOARG/Effects/Glitch2"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _FilterTime ("Filter Time", Float) = 0
        _Amount ("Amount", Range(0, 1)) = 0.1
        _Speed ("Speed", Range(0, 1)) = 0.5
    }
    SubShader
    {
        // Post-processing shaders should have these settings
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
            float _FilterTime;
            float _Amount;
            float _Speed;

            float random(float2 st)
            {
                return frac(sin(dot(st.xy, float2(12.9898, 4.1414))) * 43758.5453123);
            }

            float randomRange(float2 st, float lo, float hi)
            {
                return lo + random(st) * (hi - lo);
            }

            // 1 only when v sits within [b, t).
            float insideRange(float v, float b, float t)
            {
                return step(b, v) - step(t, v);
            }

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                float4 colour = tex2D(_MainTex, uv);

                float maxOff = _Amount / 2.0;

                // Quantised so the effect holds still for a frame at a time rather than crawling,
                // which is what makes it read as a dropped frame instead of a wobble.
                float time = floor(_FilterTime * _Speed * 60.0);

                // Horizontal slices torn sideways.
                for (float s = 0; s < 10.0 * _Amount; s++)
                {
                    float slcY = random(float2(time, 2345.0 + s));
                    float slcH = random(float2(time, 9035.0 + s)) * 0.25;
                    float hOff = randomRange(float2(time, 9625.0 + s), -maxOff, maxOff);

                    if (insideRange(uv.y, slcY, frac(slcY + slcH)) == 1.0)
                        colour = tex2D(_MainTex, float2(uv.x + hOff, uv.y));
                }

                // One colour channel pulled out of register.
                float maxColOff = _Amount / 6.0;
                float rnd = random(float2(time, 9545.0));
                float2 colOffset = float2(
                    randomRange(float2(time, 9545.0), -maxColOff, maxColOff),
                    randomRange(float2(time, 7205.0), -maxColOff, maxColOff)
                );

                if (rnd < 0.33)
                    colour.r = tex2D(_MainTex, uv + colOffset).r;
                else if (rnd < 0.66)
                    colour.g = tex2D(_MainTex, uv + colOffset).g;
                else
                    colour.b = tex2D(_MainTex, uv + colOffset).b;

                return colour;
            }
            ENDCG
        }
    }
}