Shader "JANOARG/Effects/Vignette"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Strength ("Vignette Strength", Range(0, 1)) = 0.5
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
            #pragma vertex vert_img  // Use Unity's built-in fullscreen vertex shader
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Strength;

            fixed4 frag (v2f_img i) : SV_Target  // v2f_img is Unity's built-in struct
            {
                // Sample the texture
                fixed4 colour = tex2D(_MainTex, i.uv);

                // Calculate vignette
                float2 uv = i.uv;
                uv = uv * (1.0 - uv.yx);
                float vig = uv.x * uv.y * (1.0 - _Strength);
                vig = pow(vig, _Strength);

                // Mix with black
                return lerp(colour, fixed4(0, 0, 0, 1), 1.0 - vig);
            }
            ENDCG
        }
    }
}
