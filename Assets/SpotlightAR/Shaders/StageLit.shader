// Stylised low-poly surface for stage props and the crowd.
// Wrapped diffuse from the main light, a coloured rim, and "stage glow": a fake
// light from the stage centre whose colour/strength the Stage script sets globally
// every frame. This replaces real-time spot lights, which are disabled on mobile.
Shader "SpotlightAR/Stage Lit"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.25, 0.2, 0.45, 1)
        _Ambient ("Ambient", Range(0, 1)) = 0.3
        _RimColor ("Rim Color", Color) = (1, 0.36, 0.78, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _GlowResponse ("Stage Glow Response", Range(0, 2)) = 1
        _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "StageLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Ambient;
                half4 _RimColor;
                half _RimPower;
                half _GlowResponse;
                half4 _EmissionColor;
            CBUFFER_END

            // Set once per frame by Stage.cs for every stage material.
            half4 _StageGlowColor;
            half _StageGlowLevel;
            float4 _StageGlowCenter; // xyz = world centre, w = radius
            // Set by LightEstimationApplier from the real room's brightness (0 = bright room).
            half _SpotlightRoomDarkness;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                float3 viewDir = normalize(GetCameraPositionWS() - input.positionWS);

                Light mainLight = GetMainLight();
                half wrap = saturate(dot(normal, mainLight.direction) * 0.5h + 0.5h);
                half3 diffuse = _BaseColor.rgb * (mainLight.color * wrap * 0.75h + _Ambient * (1.0h - _SpotlightRoomDarkness));

                float radius = max(_StageGlowCenter.w, 1e-3);
                half falloff = saturate(1.0 - distance(input.positionWS, _StageGlowCenter.xyz) / radius);
                half facingUp = saturate(normal.y * 0.5h + 0.6h);
                half3 glow = _StageGlowColor.rgb * (_StageGlowLevel * falloff * facingUp * _GlowResponse);

                half rim = pow(1.0h - saturate(dot(normal, viewDir)), _RimPower);
                half3 rimLight = _RimColor.rgb * rim * (0.35h + 0.65h * _StageGlowLevel);

                half3 color = diffuse + glow * (_BaseColor.rgb * 1.5h + 0.15h) + rimLight + _EmissionColor.rgb;
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        // Lets the object write depth for effects that need it (cheap, no shadows cast).
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionHCS : SV_POSITION; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half frag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
