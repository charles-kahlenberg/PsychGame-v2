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
// Excitement (_Excite, 0-1, set by ShaderBackground.Excite) turns the same
// picture up for the intro screen (Feature.SynopsisTransition): brighter
// colours, more layers flowing faster, glowing contours with pulses of light
// running down through the layers, and sparks drifting up like thoughts
// firing. At 0 it's exactly the calm look above, so dialling it down turns
// the intro's background into the response screen's with no cut.
//
// Every look-and-feel knob is a property below (Scale, Flow Speed and Warp,
// and their Excited versions, are passed on to the field pass); tweak the
// defaults here, or live in Play mode on the runtime material
// (Canvas > ShaderBackground > Image).
Shader "PsychGame/ThoughtCurrents"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        _ColorDeep ("Deep Colour", Color) = (0.2314, 0.2745, 0.4039, 1)   // #3B4667
        _ColorMid ("Mid Colour", Color) = (0.0729, 0.0988, 0.1506, 1)     // #131926
        _ColorLight ("Light Colour", Color) = (0.0729, 0.0988, 0.1506, 1) // #131926
        _LineColor ("Contour Colour", Color) = (0.2651, 0.3255, 0.6016, 1) // #445399

        _Scale ("Pattern Scale", Float) = 1.3
        _FlowSpeed ("Flow Speed", Float) = 0.02
        _Warp ("Warp Amount", Float) = 2.2
        _Layers ("Layer Count", Float) = 5
        _LineStrength ("Contour Strength", Range(0, 1)) = 0.16
        _LayerShadow ("Layer Shadow", Range(0, 1)) = 0.14
        _Vignette ("Vignette", Range(0, 1)) = 0.4

        // The same knobs at full excitement, and the effects only it has.
        _Excite ("Excitement", Range(0, 1)) = 0
        _ExciteDeep ("Excited Deep Colour", Color) = (0.3451, 0.4, 0.6588, 1)    // #5866A8
        _ExciteMid ("Excited Mid Colour", Color) = (0.1059, 0.1373, 0.2784, 1)   // #1B2347
        _ExciteLight ("Excited Light Colour", Color) = (0.0784, 0.102, 0.2196, 1) // #141A38
        _ExciteLine ("Excited Contour Colour", Color) = (0.549, 0.6196, 1, 1)    // #8C9EFF
        _GlowColor ("Pulse and Spark Colour", Color) = (0.8627, 0.8863, 1, 1)    // #DCE2FF
        _ExciteScale ("Excited Pattern Scale", Float) = 1.6
        _ExciteFlowSpeed ("Excited Flow Speed", Float) = 0.07
        _ExciteWarp ("Excited Warp Amount", Float) = 2.8
        _ExciteLayers ("Excited Layer Count", Float) = 7
        _ExciteLineStrength ("Excited Contour Strength", Range(0, 1)) = 0.6
        _ExciteVignette ("Excited Vignette", Range(0, 1)) = 0.2
        _PulseStrength ("Pulse Strength", Range(0, 1)) = 0.45
        _SparkStrength ("Spark Strength", Range(0, 1)) = 0.8

        // The noise field, rendered each frame by ShaderBackground.cs.
        [HideInInspector] _FieldTex ("Field", 2D) = "gray" {}
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
            sampler2D _FieldTex;
            float4 _FieldTex_TexelSize;
            float _Layers;
            float _LineStrength;
            float _LayerShadow;
            float _Vignette;

            float _Excite;
            float4 _ExciteDeep;
            float4 _ExciteMid;
            float4 _ExciteLight;
            float4 _ExciteLine;
            float4 _GlowColor;
            float _ExciteLayers;
            float _ExciteLineStrength;
            float _ExciteVignette;
            float _PulseStrength;
            float _SparkStrength;
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

            float3 palette(float k, float e)
            {
                float3 deep = lerp(_ColorDeep.rgb, _ExciteDeep.rgb, e);
                float3 mid = lerp(_ColorMid.rgb, _ExciteMid.rgb, e);
                float3 light = lerp(_ColorLight.rgb, _ExciteLight.rgb, e);
                return k < 0.5
                    ? lerp(deep, mid, k * 2.0)
                    : lerp(mid, light, k * 2.0 - 1.0);
            }

            // Same sine-free hash as the field pass.
            float hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // Sparks: a few cells of a grid drifting up the screen each hold
            // a soft point of light that twinkles on its own beat.
            float sparks(float2 uv, float t)
            {
                float2 p = uv * float2(_Aspect, 1.0) * 22.0;
                p.y -= t * 0.6;
                float2 cell = floor(p);
                float h = hash12(cell);
                float h2 = hash12(cell + 17.3);
                if (hash12(cell + 41.7) < 0.86) return 0.0;

                float d = length(frac(p) - 0.5 - (float2(h, h2) - 0.5) * 0.6);
                float twinkle = 0.5 + 0.5 * sin(t * (1.5 + 2.0 * h) + 6.2832 * h2);
                twinkle *= twinkle * twinkle;
                return twinkle * (smoothstep(0.12, 0.0, d) + 0.35 * smoothstep(0.35, 0.0, d));
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 centered = IN.uv - 0.5;
                float field = saturate(sampleField(IN.uv));
                float e = saturate(_Excite);
                float layers = lerp(_Layers, _ExciteLayers, e);

                // Cut the field into flat layers. The half-step offset keeps
                // the flat spots where field clamps to 0 or 1 mid-layer, so no
                // contour gets smeared across them. w is about one pixel in
                // layer units, used to anti-alias every edge.
                float x = field * layers + 0.5;
                float w = clamp(fwidth(x), 1e-4, 0.5);
                float layer = floor(x);
                float within = x - layer; // 0 at a layer's bottom edge, 1 at its top

                float stepped = layer + smoothstep(1.0 - w, 1.0, within);
                float3 col = palette(saturate(stepped / max(layers, 1.0)), e);

                // The layer above casts a soft shadow as it's approached...
                col *= 1.0 - _LayerShadow * smoothstep(0.55, 1.0, within);
                // ...and a hairline contour runs along each edge.
                float toEdge = min(within, 1.0 - within);
                float edge = 1.0 - smoothstep(0.0, 1.5 * w, toEdge);
                col = lerp(col, lerp(_LineColor.rgb, _ExciteLine.rgb, e),
                           edge * lerp(_LineStrength, _ExciteLineStrength, e));

                if (e > 0.0)
                {
                    float t = _Time.y;
                    // Contours glow softly (about 6 px out)...
                    float halo = exp(-toEdge / (6.0 * w));
                    // ...and bands of light run down through the layers, two
                    // at a time, brightest on the contours they cross.
                    float phase = frac(field * 2.0 - t * 0.22);
                    float pulse = smoothstep(0.7, 0.96, phase) * (1.0 - smoothstep(0.96, 1.0, phase));
                    col += _ExciteLine.rgb * halo * 0.25 * e;
                    col += _GlowColor.rgb * pulse * (0.25 + 0.75 * edge + 0.5 * halo) * _PulseStrength * e;
                    col += _GlowColor.rgb * sparks(IN.uv, t) * _SparkStrength * e;
                    // A little more light in the middle.
                    col *= 1.0 + 0.2 * e * (1.0 - saturate(dot(centered, centered) * 3.0));
                }

                // Quiet the corners so the eye settles on the middle.
                col *= 1.0 - lerp(_Vignette, _ExciteVignette, e) * saturate(dot(centered, centered) * 2.0);

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
