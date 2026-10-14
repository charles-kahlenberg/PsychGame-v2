// The tint half of a hovered card's glow (Feature.CardHoverGlow, see
// CardBehavior). It multiplies whatever is under it by the glow's colour,
// so the light shows on pale things too, like the response box's paper,
// where the glow itself (a pale colour laid over pale paper) barely did.
Shader "PsychGame/CardGlowTint"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
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

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend DstColor Zero

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.color = v.color;
                OUT.uv = v.texcoord;
                return OUT;
            }

            // White (no change) where the glow is clear, its colour where it's solid.
            fixed4 frag(v2f IN) : SV_Target
            {
                fixed strength = tex2D(_MainTex, IN.uv).a * IN.color.a;
                return fixed4(lerp(fixed3(1, 1, 1), IN.color.rgb, strength), 1);
            }
            ENDCG
        }
    }
}
