// The expensive half of ThoughtCurrents: the flowing noise field itself.
//
// ShaderBackground.cs blits this into a small render texture every frame
// (about 360 px tall), and ThoughtCurrents.shader then samples it at full
// screen resolution to draw the layers. The field is made of big smooth
// shapes, so it looks the same upsampled, while running this ~80-hash noise
// on a tenth of the pixels or fewer.
//
// Its inputs (_Scale, _Warp, _Aspect, _FlowTime) are copied over by
// ShaderBackground.cs from the ThoughtCurrents material, so tweak them there.
Shader "Hidden/PsychGame/ThoughtCurrentsField"
{
    Properties
    {
        _MainTex ("Unused", 2D) = "black" {}
        _Scale ("Pattern Scale", Float) = 1.3
        _Warp ("Warp Amount", Float) = 2.2
        _Aspect ("Aspect", Float) = 1.7778
        _FlowTime ("Flow Time", Float) = 0
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
            #pragma target 3.0

            #include "UnityCG.cginc"

            float _Scale;
            float _Warp;
            float _Aspect;
            float _FlowTime;

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

            float4 frag(v2f_img IN) : SV_Target
            {
                float2 p = (IN.uv - 0.5) * float2(_Aspect, 1.0) * _Scale;
                float t = _FlowTime;

                // Two rounds of domain warping: noise pushes noise around, and
                // the time offsets make the shapes fold into each other slowly.
                float2 q = float2(fbm(p + float2(0.0, t)),
                                  fbm(p + float2(5.2, 1.3) - t));
                float2 r = float2(fbm(p + _Warp * q + float2(1.7, 9.2) + 0.6 * t),
                                  fbm(p + _Warp * q + float2(8.3, 2.8) - 0.4 * t));
                float field = saturate((fbm(p + _Warp * r) - 0.2) / 0.6);

                return float4(field, field, field, 1.0);
            }
            ENDCG
        }
    }
}
