// Soft radial gradient on a quad. One shader covers three uses:
//  - lens glows / bulbs: Billboard on, additive blend
//  - light pools on the deck: Billboard off, additive blend
//  - blob shadow on the real floor: Billboard off, alpha blend, black colour
Shader "SpotlightAR/Radial Glow"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.71, 0.28, 1)
        _Intensity ("Intensity", Float) = 1
        _Falloff ("Falloff", Range(0.2, 8)) = 2
        [Toggle] _Billboard ("Face Camera", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "RadialGlow"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
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
                half _Falloff;
                float _Billboard;
                float _SrcBlend;
                float _DstBlend;
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

                float3 positionWS;
                if (_Billboard > 0.5)
                {
                    float3 centre = TransformObjectToWorld(float3(0, 0, 0));
                    float size = length(TransformObjectToWorldDir(float3(1, 0, 0), false));
                    float3 right = UNITY_MATRIX_V[0].xyz;
                    float3 up = UNITY_MATRIX_V[1].xyz;
                    positionWS = centre + (right * input.positionOS.x + up * input.positionOS.y) * size;
                }
                else
                {
                    positionWS = TransformObjectToWorld(input.positionOS.xyz);
                }

                output.positionHCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half distanceFromCentre = length(input.uv - 0.5h) * 2.0h;
                half gradient = pow(saturate(1.0h - distanceFromCentre), _Falloff) * _Intensity;
                return half4(_Color.rgb * gradient, saturate(_Color.a * gradient));
            }
            ENDHLSL
        }
    }
}
