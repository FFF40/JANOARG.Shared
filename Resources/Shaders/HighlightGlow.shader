Shader "JANOARG/HighlightGlow/Default"
{
    // Sprite-pipeline sibling of JANOARG/Highlight/Default.
    //
    // The simultaneous-note glow is a SpriteRenderer, and a SpriteRenderer only feeds a shader
    // correctly when that shader is written against the sprite pipeline. Highlight.shader is an
    // ordinary mesh shader - it has no COLOR semantic and no _RendererColor, and its _MainTex is
    // a normal material texture rather than [PerRendererData]. Driving the glow with it meant the
    // sprite's texture was not reliably bound in player builds: _MainTex fell through to the
    // shader's "white" default, and white * _Color across the quad drew the glow as a solid box
    // on Android. It also silently discarded the renderer's own tint.
    //
    // The bold highlight bar stays on Highlight.shader, which is a MeshRenderer and has no vertex
    // colour channel to read - which is exactly why these are two shaders and not one.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        // Separate alpha blend, matching Hit.shader and Highlight.shader, so this composites
        // correctly into a transparent render target (the options panel preview camera clears
        // its RenderTexture to alpha 0). Against an opaque destination dstA is already 1 and
        // srcA + 1*(1-srcA) = 1, so gameplay rendering is unchanged.
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            fixed4 _RendererColor;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                // Unity delivers the SpriteRenderer tint one of two ways depending on whether the
                // draw was batched: baked into vertex colour, or through _RendererColor with white
                // vertices. Multiplying both is what the built-in sprite shader does - exactly one
                // of them is ever non-white.
                o.color = v.color * _Color * _RendererColor;
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            // The fog coordinate is the project's lane fade: linear 0-200, matching the
            // 200 unit cull distance in LanePlayer, so it ramps across the whole visible
            // lane rather than only at spawn. Without it the glow pops in while the note
            // it belongs to fades.
            // The 1.2x is deliberate and preserved from the original Hit shader: it puts
            // the glow at full opacity within z <= 33 while the note carries on ramping,
            // so the cue stays stronger than the note it marks.
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;

                float fade = 1;
                #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
                    fade = min(max(i.fogCoord.x, 0) * 1.2, 1);
                #endif
                col.a *= fade;

                return col;
            }
            ENDCG
        }
    }
}
