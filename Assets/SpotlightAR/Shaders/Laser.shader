// Laser line for LineRenderer: a hot white-ish core with a coloured glow falloff
// across the line, fading out towards the tip. Colour comes from the LineRenderer
// vertex colours, so every laser shares one material (SRP Batcher friendly).
Shader "SpotlightAR/Laser"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Float) = 1.4
        _CoreWidth ("Core Width", Range(0.01, 1)) = 0.25
        _GlowFalloff ("Glow Falloff", Range(0.5, 8)) = 2.5
        _TipFade ("Tip Fade Start", Range(0, 1)) = 0.7
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Laser"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _CoreWidth;
                half _GlowFalloff;
                half _TipFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half across = abs(input.uv.y - 0.5h) * 2.0h;
                half core = 1.0h - smoothstep(0.0h, _CoreWidth, across);
                half glow = pow(saturate(1.0h - across), _GlowFalloff);
                half tip = 1.0h - smoothstep(_TipFade, 1.0h, input.uv.x);

                half3 tint = input.color.rgb * _Color.rgb;
                half3 rgb = (tint * glow + lerp(tint, 1.0h, 0.6h) * core) * (_Intensity * tip * input.color.a);
                return half4(rgb, 0.0h);
            }
            ENDHLSL
        }
    }
}
