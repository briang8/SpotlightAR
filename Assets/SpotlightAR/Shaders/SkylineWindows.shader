// City buildings for the Rooftop stage. Each side face gets a procedural grid of windows,
// some lit with a warm glow and a slow flicker. No textures, one material for every building.
Shader "SpotlightAR/Skyline Windows"
{
    Properties
    {
        _BaseColor ("Building Color", Color) = (0.08, 0.11, 0.2, 1)
        _WindowColor ("Window Color", Color) = (1, 0.78, 0.45, 1)
        _Columns ("Window Columns", Float) = 4
        _Rows ("Window Rows", Float) = 8
        _LitRatio ("Lit Windows", Range(0, 1)) = 0.35
        _Intensity ("Window Intensity", Float) = 1.2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Skyline"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _WindowColor;
                half _Columns;
                half _Rows;
                half _LitRatio;
                half _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float seed : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                // Each building gets a different window pattern from its position.
                float3 origin = TransformObjectToWorld(float3(0, 0, 0));
                output.seed = frac(origin.x * 7.31 + origin.z * 3.17);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                half shade = 0.65h + 0.35h * saturate(normal.y * 0.5h + 0.5h);
                half3 color = _BaseColor.rgb * shade;

                half side = 1.0h - step(0.5h, abs(normal.y));
                float2 grid = input.uv * float2(_Columns, _Rows);
                float2 cell = floor(grid);
                float2 local = frac(grid);
                half window = step(0.25, local.x) * step(local.x, 0.75) * step(0.3, local.y) * step(local.y, 0.78);

                float random = Hash(cell + input.seed * 17.0 + normal.xz * 3.0);
                half lit = step(1.0 - _LitRatio, random);
                half flicker = 0.85h + 0.15h * sin(_Time.y * (0.4 + random) + random * 30.0);

                color += _WindowColor.rgb * (window * lit * side * flicker * _Intensity);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
