// Procedural rings on a quad, no textures: an animated dashed outer ring, an optional
// solid inner ring and a centre dot. Used for the placement reticle and the neon
// trim around the edge of the Club Neon deck.
Shader "SpotlightAR/Dashed Ring"
{
    Properties
    {
        _Color ("Outer Color", Color) = (1, 0.71, 0.28, 1)
        _Intensity ("Intensity", Float) = 1
        _Radius ("Outer Radius", Range(0, 1)) = 0.96
        _Thickness ("Outer Thickness", Range(0.001, 0.5)) = 0.025
        _Dashes ("Dash Count (0 = solid)", Float) = 36
        _Duty ("Dash Fill", Range(0.05, 1)) = 0.55
        _Speed ("Dash Speed", Float) = 0.15
        _InnerColor ("Inner Color", Color) = (1, 1, 1, 1)
        _InnerRadius ("Inner Radius", Range(0, 1)) = 0
        _InnerThickness ("Inner Thickness", Range(0.001, 0.5)) = 0.03
        _DotRadius ("Dot Radius", Range(0, 0.5)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "DashedRing"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _Radius;
                half _Thickness;
                half _Dashes;
                half _Duty;
                half _Speed;
                half4 _InnerColor;
                half _InnerRadius;
                half _InnerThickness;
                half _DotRadius;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float Band(float r, float outer, float thickness, float aa)
            {
                return smoothstep(outer - thickness - aa, outer - thickness, r) * (1.0 - smoothstep(outer - aa, outer, r));
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = (input.uv - 0.5) * 2.0;
                float r = length(p);
                float aa = fwidth(r) * 1.5;

                float outer = Band(r, _Radius, _Thickness, aa);
                if (_Dashes > 0.5)
                {
                    float angle = atan2(p.y, p.x) / (2.0 * PI) + 0.5;
                    float dash = frac(angle * _Dashes + _Time.y * _Speed);
                    outer *= smoothstep(0.0, 0.06, dash) * (1.0 - smoothstep(_Duty - 0.06, _Duty, dash));
                }

                float inner = _InnerRadius > 0.0 ? Band(r, _InnerRadius, _InnerThickness, aa) : 0.0;
                float centreDot = _DotRadius > 0.0 ? 1.0 - smoothstep(_DotRadius - aa, _DotRadius, r) : 0.0;

                float outerAlpha = outer * _Color.a;
                float innerAlpha = saturate(inner + centreDot) * _InnerColor.a;
                float alpha = saturate((outerAlpha + innerAlpha) * _Intensity);
                float3 rgb = (outerAlpha * _Color.rgb + innerAlpha * _InnerColor.rgb) / max(outerAlpha + innerAlpha, 1e-4);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
