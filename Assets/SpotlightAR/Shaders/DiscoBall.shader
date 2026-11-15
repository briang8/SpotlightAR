// Mirror-tile disco ball on the built-in sphere, no textures.
// The sphere is split into latitude/longitude tiles; each tile gets one flat normal
// (faceted look), dark grout lines, a specular glint and a random twinkle whose
// amount follows _Sparkle (set by NeonStage from the singer's level).
Shader "SpotlightAR/Disco Ball"
{
    Properties
    {
        _BaseColor ("Mirror Color", Color) = (0.72, 0.74, 0.84, 1)
        _SparkleColor ("Sparkle Color", Color) = (1, 0.9, 1, 1)
        _Rows ("Tile Rows", Float) = 12
        _Columns ("Tile Columns", Float) = 24
        _Sparkle ("Sparkle", Range(0, 1)) = 0.2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "DiscoBall"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _SparkleColor;
                half _Rows;
                half _Columns;
                half _Sparkle;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalOS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            float Hash(float2 cell)
            {
                return frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                output.normalOS = input.normalOS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalOS);
                float latitude = acos(clamp(n.y, -1.0, 1.0)) / PI;   // 0..1 top to bottom
                float longitude = atan2(n.z, n.x) / (2.0 * PI) + 0.5; // 0..1 around

                float2 grid = float2(longitude * _Columns, latitude * _Rows);
                float2 cell = floor(grid);
                float2 local = frac(grid);
                float grout = step(0.08, local.x) * step(0.08, local.y) * step(local.x, 0.92) * step(local.y, 0.92);

                // One flat normal per tile gives the faceted mirror look.
                float theta = (cell.y + 0.5) / _Rows * PI;
                float phi = ((cell.x + 0.5) / _Columns - 0.5) * 2.0 * PI;
                float3 facetOS = float3(sin(theta) * cos(phi), cos(theta), sin(theta) * sin(phi));
                float3 facet = normalize(TransformObjectToWorldNormal(facetOS));

                Light mainLight = GetMainLight();
                float3 viewDir = normalize(GetCameraPositionWS() - input.positionWS);
                half diffuse = saturate(dot(facet, mainLight.direction)) * 0.6h + 0.4h;
                half glint = pow(saturate(dot(reflect(-viewDir, facet), mainLight.direction)), 48.0h);

                float random = Hash(cell);
                half twinkle = pow(saturate(sin(_Time.y * (2.0 + random * 6.0) + random * 40.0) * 0.5 + 0.5), 14.0) * _Sparkle;

                half3 color = _BaseColor.rgb * diffuse * (0.65h + 0.35h * random) + glint + twinkle * _SparkleColor.rgb * 2.0h;
                color *= lerp(0.18h, 1.0h, grout);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
