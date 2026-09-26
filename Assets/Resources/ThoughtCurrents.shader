// "Thought Currents": the response screen's background (Feature.ShaderBackground).
//
// A slowly flowing field of domain-warped noise, cut into a few flat layers
// like stacked paper. Each layer casts a soft shadow onto the one below it,
// and a faint contour line traces every edge, so it reads like a topographic
// map of a mind at work. It's meant to sit behind the player while they
// think: muted colours, low contrast, slow morphing rather than spinning, and
// a vignette that keeps the edges quiet.
//
// For speed it runs in two halves. The flowing noise is the expensive part,
// so ShaderBackground.cs renders it small (ThoughtCurrentsField.shader) into
// _FieldTex. This shader samples that smoothly at full resolution and does
// the cheap part: layers, shadows, contours and vignette, crisp at any size.
//
// Every look-and-feel knob is a property below (Scale, Flow Speed and Warp
// are passed on to the field pass); tweak the defaults here, or live in Play
// mode on the runtime material (Canvas > ShaderBackground > Image).
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

        // The noise field, rendered each frame by ShaderBackground.cs.
        [HideInInspector] _FieldTex ("Field", 2D) = "gray" {}

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
            sampler2D _FieldTex;
            float4 _FieldTex_TexelSize;
            float _Layers;
            float _LineStrength;
            float _LayerShadow;
            float _Vignette;

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

            // Cubic B-spline weights for bicubic sampling.
            float4 cubicWeights(float v)
            {
                float4 n = float4(1.0, 2.0, 3.0, 4.0) - v;
                float4 c = n * n * n;
                float x = c.x;
                float y = c.y - 4.0 * c.x;
                float z = c.z - 4.0 * c.y + 6.0 * c.x;
                float w = 6.0 - x - y - z;
                return float4(x, y, z, w) * (1.0 / 6.0);
            }

            // Smooth (bicubic) lookup of the small field texture in 4 bilinear
            // taps. Plain bilinear would leave faint kinks in the contours
            // where it crosses texel boundaries.
            float sampleField(float2 uv)
            {
                float2 texSize = _FieldTex_TexelSize.zw;
                float2 invSize = _FieldTex_TexelSize.xy;

                uv = uv * texSize - 0.5;
                float2 fxy = frac(uv);
                uv -= fxy;

                float4 xw = cubicWeights(fxy.x);
                float4 yw = cubicWeights(fxy.y);

                float4 c = uv.xxyy + float2(-0.5, 1.5).xyxy;
                float4 s = float4(xw.xz + xw.yw, yw.xz + yw.yw);
                float4 offset = (c + float4(xw.yw, yw.yw) / s) * invSize.xxyy;

                float s0 = tex2D(_FieldTex, offset.xz).r;
                float s1 = tex2D(_FieldTex, offset.yz).r;
                float s2 = tex2D(_FieldTex, offset.xw).r;
                float s3 = tex2D(_FieldTex, offset.yw).r;

                float sx = s.x / (s.x + s.y);
                float sy = s.z / (s.z + s.w);
                return lerp(lerp(s3, s2, sx), lerp(s1, s0, sx), sy);
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
                float field = saturate(sampleField(IN.uv));

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
