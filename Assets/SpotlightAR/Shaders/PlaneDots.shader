// Visual for detected AR planes: a faint amber fill with a world-space dot grid that
// fades with distance from the camera, so the player can see where the floor or
// table has been found without hiding the real room.
Shader "SpotlightAR/Plane Dots"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.71, 0.28, 1)
        _Fill ("Fill Opacity", Range(0, 1)) = 0.06
        _DotOpacity ("Dot Opacity", Range(0, 1)) = 0.7
        _Spacing ("Dot Spacing (m)", Float) = 0.08
        _DotSize ("Dot Size", Range(0.01, 0.5)) = 0.12
        _FadeDistance ("Fade Distance (m)", Float) = 2.5
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "PlaneDots"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Fill;
                half _DotOpacity;
                float _Spacing;
                half _DotSize;
                float _FadeDistance;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 cell = frac(input.positionWS.xz / _Spacing) - 0.5;
                float d = length(cell);
                float aa = fwidth(d) * 1.5;
                float dotMask = 1.0 - smoothstep(_DotSize - aa, _DotSize, d);

                float cameraDistance = distance(input.positionWS, GetCameraPositionWS());
                float fade = 1.0 - smoothstep(_FadeDistance * 0.5, _FadeDistance, cameraDistance);

                float alpha = saturate(_Fill + dotMask * _DotOpacity) * fade * _Color.a;
                return half4(_Color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
