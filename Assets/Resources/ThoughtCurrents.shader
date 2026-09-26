// "Thought Currents": the response screen's background (Feature.ShaderBackground).
//
// A slowly flowing field of domain-warped noise, cut into a few flat layers
// like stacked paper. Each layer casts a soft shadow onto the one below it,
// and a faint contour line traces every edge, so it reads like a topographic
// map of a mind at work. It's meant to sit behind the player while they
// think: muted colours, low contrast, slow morphing rather than spinning, and
// a vignette that keeps the edges quiet.
//
// Every look-and-feel knob is a property below; tweak the defaults here, or
// live in Play mode on the runtime material (Canvas > ShaderBackground > Image).
Shader "PsychGame/ThoughtCurrents"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        _ColorDeep ("Deep Colour", Color) = (0.10, 0.11, 0.21, 1)
        _ColorMid ("Mid Colour", Color) = (0.25, 0.29, 0.50, 1)
        _ColorLight ("Light Colour", Color) = (0.38, 0.55, 0.54, 1)
        _LineColor ("Contour Colour", Color) = (0.93, 0.87, 0.78, 1)

        _Scale ("Pattern Scale", Float) = 1.3
        _FlowSpeed ("Flow Speed", Float) = 0.02
        _Warp ("Warp Amount", Float) = 2.2
        _Layers ("Layer Count", Float) = 5
        _LineStrength ("Contour Strength", Range(0, 1)) = 0.16
        _LayerShadow ("Layer Shadow", Range(0, 1)) = 0.14
        _Vignette ("Vignette", Range(0, 1)) = 0.4

        // Width / height of the rect, set every frame by ShaderBackground.cs
        // so the pattern isn't stretched on wide screens.
        [HideInInspector] _Aspect ("Aspect", Float) = 1.7778

        // Standard UI boilerplate, so masks and RectMask2D still work.
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            float4 _ClipRect;

            float4 _ColorDeep;
            float4 _ColorMid;
            float4 _ColorLight;
            float4 _LineColor;
            float _Scale;
            float _FlowSpeed;
            float _Warp;
            float _Layers;
            float _LineStrength;
            float _LayerShadow;
            float _Vignette;
            float _Aspect;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.uv = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            // Sine-free hash, so it looks the same on every GPU (WebGL included).
            float hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0); // quintic: no grid creases

                float a = hash12(i);
                float b = hash12(i + float2(1, 0));
                float c = hash12(i + float2(0, 1));
                float d = hash12(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p)
            {
                const float2x2 turn = float2x2(0.8, -0.6, 0.6, 0.8);
                float sum = 0.0;
                float amp = 0.5;
                for (int i = 0; i < 4; i++)
                {
                    sum += amp * valueNoise(p);
                    p = mul(turn, p) * 2.03 + 17.1;
                    amp *= 0.42; // fades fine detail fast, for big calm shapes
                }
                return sum;
            }

            float3 palette(float k)
            {
                return k < 0.5
                    ? lerp(_ColorDeep.rgb, _ColorMid.rgb, k * 2.0)
                    : lerp(_ColorMid.rgb, _ColorLight.rgb, k * 2.0 - 1.0);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 centered = IN.uv - 0.5;
                float2 p = centered * float2(_Aspect, 1.0) * _Scale;
                float t = _Time.y * _FlowSpeed;

                // Two rounds of domain warping: noise pushes noise around, and
                // the time offsets make the shapes fold into each other slowly.
                float2 q = float2(fbm(p + float2(0.0, t)),
                                  fbm(p + float2(5.2, 1.3) - t));
                float2 r = float2(fbm(p + _Warp * q + float2(1.7, 9.2) + 0.6 * t),
                                  fbm(p + _Warp * q + float2(8.3, 2.8) - 0.4 * t));
                float field = saturate((fbm(p + _Warp * r) - 0.2) / 0.6);

                // Cut the field into flat layers. The half-step offset keeps
                // the flat spots where field clamps to 0 or 1 mid-layer, so no
                // contour gets smeared across them. w is about one pixel in
                // layer units, used to anti-alias every edge.
                float x = field * _Layers + 0.5;
                float w = clamp(fwidth(x), 1e-4, 0.5);
                float layer = floor(x);
                float within = x - layer; // 0 at a layer's bottom edge, 1 at its top

                float stepped = layer + smoothstep(1.0 - w, 1.0, within);
                float3 col = palette(saturate(stepped / max(_Layers, 1.0)));

                // The layer above casts a soft shadow as it's approached...
                col *= 1.0 - _LayerShadow * smoothstep(0.55, 1.0, within);
                // ...and a hairline contour runs along each edge.
                float edge = 1.0 - smoothstep(0.0, 1.5 * w, min(within, 1.0 - within));
                col = lerp(col, _LineColor.rgb, edge * _LineStrength);

                // Quiet the corners so the eye settles on the middle.
                col *= 1.0 - _Vignette * saturate(dot(centered, centered) * 2.0);

                fixed4 color = fixed4(col, 1.0) * IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
