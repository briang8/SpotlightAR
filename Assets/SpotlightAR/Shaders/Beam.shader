// Fake volumetric spotlight beam on a single quad.
// The quad turns around its own Y axis to face the camera (axial billboard), and the
// fragment shader cuts it into a soft trapezoid that fades along its length, so from
// any angle it reads as a cone of light. Additive, no depth write, 2 triangles.
Shader "SpotlightAR/Beam"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.71, 0.28, 1)
        _Intensity ("Intensity", Float) = 0.6
        _StartWidth ("Width At Lamp (0-1)", Range(0.01, 1)) = 0.12
        _EdgeSoftness ("Edge Softness", Range(0.05, 1)) = 0.7
        _LengthFade ("Length Fade", Range(0.1, 4)) = 1.4
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Beam"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
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
                half _StartWidth;
                half _EdgeSoftness;
                half _LengthFade;
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

                float3 centre = TransformObjectToWorld(float3(0, 0, 0));
                float3 axis = TransformObjectToWorldDir(float3(0, 1, 0), false);   // keeps length scale
                float3 sideRef = TransformObjectToWorldDir(float3(1, 0, 0), false); // keeps width scale
                float width = length(sideRef);

                float3 axisN = normalize(axis);
                float3 toCamera = normalize(GetCameraPositionWS() - centre);
                float3 side = cross(axisN, toCamera);
                float sideLength = length(side);
                side = sideLength > 1e-4 ? side / sideLength : normalize(sideRef);

                float3 positionWS = centre + axis * input.positionOS.y + side * (width * input.positionOS.x);
                output.positionHCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half along = input.uv.y; // 0 at the lamp, 1 at the far end
                half halfWidth = lerp(_StartWidth, 1.0h, along);
                half across = abs(input.uv.x - 0.5h) * 2.0h / max(halfWidth, 1e-3h);
                half edge = smoothstep(0.0h, _EdgeSoftness, 1.0h - across);
                half fade = pow(saturate(1.0h - along), _LengthFade) * smoothstep(0.0h, 0.06h, along);
                return half4(_Color.rgb * (_Intensity * edge * edge * fade), 0.0h);
            }
            ENDHLSL
        }
    }
}
