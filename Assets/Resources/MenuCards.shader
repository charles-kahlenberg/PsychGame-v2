// The title screen's background (Feature.MenuCardBackground): columns of
// card backs scrolling endlessly, every other column up and the rest down.
//
// It's one full-screen quad: each pixel works out which card it's on, so
// there are no per-card objects (only the buttons' places, for their
// shadows, come from script each frame). The card
// backs come from one atlas, Resources/MenuCardBacks.png, ten 284x380
// flat backs side by side (set by MenuCardBackground.cs as _CardTex): the
// seven areas plus the question card three times (at 1, 4 and 7), so it
// turns up three times as often. MenuIntro.AreaCards lists the rest. A
// vignette darkens the edges, a soft light brightens the middle, and the
// title and buttons cast the same drop shadow onto the cards.
//
// Every look-and-feel knob is a property below; tweak the defaults here, or
// live in Play mode on the runtime material (Canvas > Panel > Image).
Shader "PsychGame/MenuCards"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        _CardTex ("Card Backs Atlas", 2D) = "white" {}
        _CardCount ("Cards In Atlas", Float) = 10
        _CardSaturation ("Card Saturation (1 as drawn, 0 grey)", Range(0, 1)) = 0.7
        _BackColor ("Gap Colour", Color) = (0.0078, 0.0588, 0.1804, 1) // #020F2E
        _CardHeight ("Card Height (screen heights)", Float) = 0.3
        _Gap ("Gap (card heights)", Float) = 0.056
        _Speed ("Scroll Speed (card heights/s)", Float) = 0.15
        _Stagger ("Column Stagger (cards)", Float) = 0.25

        // For MenuNewGameTransition (Feature.MenuNewGameTransition), whose
        // cards fall away: 0 hides the grid, leaving the gap colour; and a
        // copy of this material with Loose on draws the falling cards
        // themselves, each a quad whose UVs pick its card from the atlas.
        [HideInInspector] _ShowCards ("Show Cards", Float) = 1
        [HideInInspector] _Loose ("Loose Cards", Float) = 0

        // A white light at the centre of the screen, brightening what's near it.
        _LightColor ("Centre Light Colour", Color) = (1, 1, 1, 1)
        _LightStrength ("Centre Light Strength", Range(0, 1)) = 0.06
        _LightReach ("Centre Light Reach (screen heights)", Float) = 0.55

        // The title's and buttons' drop shadows, cast onto the cards only
        // (the gaps stay solid), all offset the same way, in screen heights.
        _ShadowColor ("Shadow Colour", Color) = (0, 0, 0, 1)
        _ShadowOffset ("Shadow Offset (x, y)", Vector) = (0.012, -0.018, 0, 0)
        _ShadowStrength ("Title Shadow Strength", Range(0, 1)) = 0.6
        _ShadowSoftness ("Title Shadow Softness (mip level)", Range(0, 6)) = 2
        _ButtonShadowStrength ("Button Shadow Strength", Range(0, 1)) = 0.6
        // Matches the title's blur at its default softness.
        _ButtonShadowSoftness ("Button Shadow Softness (screen heights)", Range(0.0005, 0.1)) = 0.001

        // The title's art and where it sits (centre x, y from the screen's
        // centre and width, height, all in screen heights), set by
        // MenuCardBackground.cs. No title, no shadow.
        [HideInInspector] _TitleTex ("Title", 2D) = "black" {}
        [HideInInspector] _TitleRect ("Title Rect", Vector) = (0, 0, 0, 0)
        // How many buttons, and each one's centre and half-size in screen
        // heights from the screen's centre (_Buttons[8]), set every frame
        // by MenuCardBackground.cs.
        [HideInInspector] _ButtonCount ("Buttons", Float) = 0

        // Darkens toward the edges. Distance is 0 at the centre and 1 at
        // the middle of the top and bottom edges (the corners are further).
        _VignetteColor ("Vignette Colour", Color) = (0, 0, 0, 1)
        _VignetteStrength ("Vignette Strength", Range(0, 1)) = 0.8
        _VignetteStart ("Vignette Start (distance)", Range(0, 2)) = 0.5
        _VignetteSoftness ("Vignette Softness (distance)", Range(0.01, 2)) = 0.8
        _VignetteRoundness ("Vignette Roundness (0 screen-shaped, 1 circle)", Range(0, 1)) = 0

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

            sampler2D _CardTex;
            float4 _CardTex_TexelSize;
            float _CardCount;
            float _CardSaturation;
            float4 _BackColor;
            float _CardHeight;
            float _Gap;
            float _Speed;
            float _Stagger;
            float _ShowCards;
            float _Loose;
            float4 _ShadowColor;
            float _ShadowStrength;
            float _ShadowSoftness;
            sampler2D _TitleTex;
            float4 _TitleRect;
            float4 _LightColor;
            float _LightStrength;
            float _LightReach;
            float _ButtonShadowStrength;
            float4 _ShadowOffset;
            float _ButtonShadowSoftness;
            float _ButtonCount;
            float4 _Buttons[8];
            float4 _VignetteColor;
            float _VignetteStrength;
            float _VignetteStart;
            float _VignetteSoftness;
            float _VignetteRoundness;

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

            // 0-1, the same every frame for the same whole-number inputs.
            // No sin(), which loses precision on some GPUs as rows climb.
            float Hash(float2 p)
            {
                float3 p3 = frac(p.xyx * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // A whole number from 0 to n - 1.
            float Pick(float2 p, float n)
            {
                return min(floor(Hash(p) * n), n - 1.0);
            }

            // Which card a slot shows. Neighbouring columns slide past each
            // other, so to never show the same card side by side they share
            // no cards at all: each column draws from its own run of 3 of
            // the cards, starting 3 after the last column's; the sets repeat
            // every 10 columns, about a screen's width. The question cards
            // are 3 or more apart so no column holds two, but they can meet
            // across a column. Down a column the order is random, but never the
            // same card twice in a row: even rows are free picks; odd rows
            // pick from the cards their two neighbours aren't showing.
            float CardAt(float column, float row)
            {
                const float perColumn = 3.0;
                float first = 3.0 * column;

                float slot;
                if (frac(row * 0.5) < 0.25)
                {
                    slot = Pick(float2(column, row), perColumn);
                }
                else
                {
                    float a = Pick(float2(column, row - 1.0), perColumn);
                    float b = Pick(float2(column, row + 1.0), perColumn);
                    float lo = min(a, b), hi = max(a, b);
                    slot = Pick(float2(column, row), perColumn - (a == b ? 1.0 : 2.0));
                    if (slot >= lo) slot += 1.0;
                    if (a != b && slot >= hi) slot += 1.0;
                }

                // Wrapped from the middle of a step (+0.5): GPU division
                // rounds, and an exact 20 / 10 = 1.9999 would pick card 10.
                return floor(frac((first + slot + 0.5) / _CardCount) * _CardCount);
            }

            // Scrolls a point with its column: every other column up, the
            // rest down, each a little further along than the last.
            float2 Scroll(float2 p, float2 cell, out float column)
            {
                column = floor(p.x / cell.x);
                float direction = fmod(abs(column), 2.0) < 0.5 ? 1.0 : -1.0;
                p.y += direction * _Speed * _Time.y + column * _Stagger * cell.y;
                return p;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                if (_Loose > 0.5)
                {
                    fixed4 loose = tex2D(_CardTex, IN.uv);
                    loose.rgb = lerp(dot(loose.rgb, float3(0.2126, 0.7152, 0.0722)), loose.rgb, _CardSaturation);
                    return loose * IN.color;
                }

                // In card heights, with x stretched to the screen's shape so
                // cards keep their proportions. The quad fills the screen.
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 screenP = float2(IN.uv.x * aspect, IN.uv.y) / _CardHeight;

                float cardWidth = _CardTex_TexelSize.z / (_CardCount * _CardTex_TexelSize.w);
                float2 cell = float2(cardWidth, 1.0) + _Gap;

                float column;
                float2 p = Scroll(screenP, cell, column);
                float row = floor(p.y / cell.y);

                // Position on this cell's card, 0-1 across it.
                float2 local = (p - float2(column, row) * cell - _Gap * 0.5) / float2(cardWidth, 1.0);
                bool onCard = all(local >= 0.0) && all(local <= 1.0);

                float index = CardAt(column, row);

                // Gradients taken before the per-card jump, so the mip level
                // doesn't spike along card edges.
                float2 uv = float2((index + saturate(local.x)) / _CardCount, saturate(local.y));
                float2 gradScale = 1.0 / float2(cardWidth * _CardCount, 1.0);
                fixed4 card = tex2Dgrad(_CardTex, uv, ddx(p) * gradScale, ddy(p) * gradScale);
                card.a *= onCard * _ShowCards;
                card.rgb = lerp(dot(card.rgb, float3(0.2126, 0.7152, 0.0722)), card.rgb, _CardSaturation);

                // In screen heights from the centre, where the light is.
                float2 centred = (IN.uv - 0.5) * float2(aspect, 1.0);

                // Shadows: this spot is in shadow if the title or a button
                // covers the point the offset away.
                float2 blocker = centred - _ShadowOffset.xy;

                // The title's alpha there, blurred, darkens the card.
                float2 titleUV = (blocker - _TitleRect.xy) / max(_TitleRect.zw, 1e-5) + 0.5;
                float inTitle = all(titleUV >= 0.0) && all(titleUV <= 1.0);
                float shadow = tex2Dlod(_TitleTex, float4(titleUV, 0.0, _ShadowSoftness)).a * inTitle;
                card.rgb = lerp(card.rgb, _ShadowColor.rgb, shadow * _ShadowStrength);

                // The buttons' too, as soft rounded boxes, their corners
                // half their half-height.
                float buttonShadow = 0.0;
                for (int b = 0; b < 8; b++)
                {
                    if (b >= _ButtonCount) break;
                    float4 button = _Buttons[b];
                    float radius = 0.5 * button.w;
                    float2 d = abs(blocker - button.xy) - (button.zw - radius);
                    float boxDist = length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - radius;
                    buttonShadow = max(buttonShadow, 1.0 - smoothstep(-_ButtonShadowSoftness, _ButtonShadowSoftness, boxDist));
                }
                buttonShadow *= _ButtonShadowStrength;
                card.rgb = lerp(card.rgb, _ShadowColor.rgb, buttonShadow);

                fixed4 color = fixed4(lerp(_BackColor.rgb, card.rgb, card.a), 1.0);

                // The light, screened on (it brightens without washing out),
                // except where a button's shadow on a card blocks it.
                float reach = saturate(1.0 - length(centred) / _LightReach);
                float light = _LightStrength * reach * reach * (1.0 - buttonShadow * card.a);
                color.rgb = 1.0 - (1.0 - color.rgb) * (1.0 - light * _LightColor.rgb);

                float2 fromCentre = (IN.uv - 0.5) * 2.0 * float2(lerp(1.0, aspect, _VignetteRoundness), 1.0);
                float vignette = smoothstep(_VignetteStart, _VignetteStart + _VignetteSoftness, length(fromCentre));
                color.rgb = lerp(color.rgb, _VignetteColor.rgb, vignette * _VignetteStrength);

                color *= IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
