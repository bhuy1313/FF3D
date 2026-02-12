Shader "Custom/URP/TriplanarUnlit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Texture", 2D) = "white" {}
        [MainColor]   _Color   ("Tint", Color) = (1,1,1,1)

        _Brightness      ("Brightness", Range(0,2)) = 1
        _Tiling          ("Tiles Per Meter", Float) = 1
        _BlendSharpness  ("Blend Sharpness", Range(1,16)) = 6
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "Queue"="Geometry"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // URP includes
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _Color;
                float  _Brightness;
                float  _Tiling;
                float  _BlendSharpness;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 worldPos    : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float3 worldPos = TransformObjectToWorld(IN.positionOS);
                OUT.positionHCS = TransformWorldToHClip(worldPos);
                OUT.worldPos = worldPos;
                OUT.worldNormal = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 p = IN.worldPos * _Tiling;
                float3 n = normalize(IN.worldNormal);

                // blend weights from normal
                float3 w = pow(abs(n), _BlendSharpness);
                w /= (w.x + w.y + w.z + 1e-5);

                // 3 planar projections
                half4 cx = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.zy); // X-facing uses ZY
                half4 cy = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.xz); // Y-facing uses XZ
                half4 cz = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.xy); // Z-facing uses XY

                half4 col = cx * w.x + cy * w.y + cz * w.z;

                col *= _Color;
                col.rgb *= _Brightness;

                return col;
            }
            ENDHLSL
        }
    }
}
