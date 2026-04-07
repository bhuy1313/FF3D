Shader "Custom/URP/WindLit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        _Diffuse ("Diffuse (Legacy)", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1,1,1,1)
        [Toggle] _UseLegacyDiffuse ("Use Legacy Diffuse", Float) = 0
        [Toggle] _AlphaClip ("Alpha Clipping", Float) = 1
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [Enum(Off,0,Front,1,Back,2)] _Cull ("Cull", Float) = 0
        [Toggle] _ReceiveShadows ("Receive Shadows", Float) = 1
        _WindSpeed ("Wind Speed", Float) = 1
        _WindIntensity ("Wind Intensity", Float) = 0.05
        _WindDir ("Wind Direction 1", Vector) = (1,0,0,0)
        _WindDir2 ("Wind Direction 2", Vector) = (0,0,1,0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="TransparentCutout"
            "Queue"="AlphaTest"
            "UniversalMaterialType"="Lit"
        }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Blend One Zero
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_Diffuse);
            SAMPLER(sampler_Diffuse);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _Diffuse_ST;
                float4 _BaseColor;
                float4 _WindDir;
                float4 _WindDir2;
                float _UseLegacyDiffuse;
                float _AlphaClip;
                float _Cutoff;
                float _ReceiveShadows;
                float _WindSpeed;
                float _WindIntensity;
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
                float4 positionCS : SV_POSITION;
                float2 uvBase : TEXCOORD0;
                float2 uvDiffuse : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                half3 normalWS : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
                half fogFactor : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float2 NormalizeOrZero(float2 value)
            {
                float lengthSq = dot(value, value);
                if (lengthSq <= 1e-5)
                {
                    return 0;
                }

                return value * rsqrt(lengthSq);
            }

            float3 ApplyWindOffset(float3 positionWS, float2 uv)
            {
                float2 windDir1 = NormalizeOrZero(_WindDir.xz);
                float2 windDir2 = NormalizeOrZero(_WindDir2.xz);
                float windMask = saturate(uv.y);
                windMask *= windMask;

                float primaryPhase = dot(positionWS.xz, float2(0.11, 0.07));
                float secondaryPhase = dot(positionWS.xz, float2(-0.05, 0.09));
                float primaryWave = sin(primaryPhase + (_Time.y * _WindSpeed));
                float secondaryWave = sin(secondaryPhase - (_Time.y * (_WindSpeed * 1.37)) + 1.5707963);
                float2 windOffsetXZ = (windDir1 * primaryWave + windDir2 * secondaryWave * 0.5) * (_WindIntensity * windMask);

                return positionWS + float3(windOffsetXZ.x, 0.0, windOffsetXZ.y);
            }

            half4 SampleAlbedo(float2 uvBase, float2 uvDiffuse)
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvBase);
                half4 diffuseSample = SAMPLE_TEXTURE2D(_Diffuse, sampler_Diffuse, uvDiffuse);
                return lerp(baseSample, diffuseSample, saturate(_UseLegacyDiffuse));
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                float3 displacedWS = ApplyWindOffset(positionInputs.positionWS, input.uv);

                output.positionCS = TransformWorldToHClip(displacedWS);
                output.uvBase = TRANSFORM_TEX(input.uv, _BaseMap);
                output.uvDiffuse = TRANSFORM_TEX(input.uv, _Diffuse);
                output.positionWS = displacedWS;
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                output.shadowCoord = TransformWorldToShadowCoord(displacedWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 albedoSample = SampleAlbedo(input.uvBase, input.uvDiffuse);
                half alpha = albedoSample.a * _BaseColor.a;
                if (_AlphaClip > 0.5)
                {
                    clip(alpha - _Cutoff);
                }

                half3 albedo = albedoSample.rgb * _BaseColor.rgb;
                half3 normalWS = normalize(input.normalWS);
                half3 bakedGI = SampleSH(normalWS);

                Light mainLight = GetMainLight(input.shadowCoord);
                half mainShadow = lerp(1.0h, mainLight.shadowAttenuation, saturate(_ReceiveShadows));
                half mainNdotL = saturate(dot(normalWS, mainLight.direction));
                half3 lighting = bakedGI + (mainLight.color * (mainNdotL * mainShadow));

                #if defined(_ADDITIONAL_LIGHTS)
                uint additionalLightsCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < additionalLightsCount; ++lightIndex)
                {
                    Light light = GetAdditionalLight(lightIndex, input.positionWS);
                    half nDotL = saturate(dot(normalWS, light.direction));
                    lighting += light.color * (nDotL * light.distanceAttenuation * light.shadowAttenuation);
                }
                #endif

                half3 finalColor = albedo * lighting;
                finalColor = MixFog(finalColor, input.fogFactor);
                return half4(finalColor, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_Diffuse);
            SAMPLER(sampler_Diffuse);

            float3 _LightDirection;
            float3 _LightPosition;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _Diffuse_ST;
                float4 _BaseColor;
                float4 _WindDir;
                float4 _WindDir2;
                float _UseLegacyDiffuse;
                float _AlphaClip;
                float _Cutoff;
                float _ReceiveShadows;
                float _WindSpeed;
                float _WindIntensity;
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
                float4 positionCS : SV_POSITION;
                float2 uvBase : TEXCOORD0;
                float2 uvDiffuse : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float2 NormalizeOrZero(float2 value)
            {
                float lengthSq = dot(value, value);
                if (lengthSq <= 1e-5)
                {
                    return 0;
                }

                return value * rsqrt(lengthSq);
            }

            float3 ApplyWindOffset(float3 positionWS, float2 uv)
            {
                float2 windDir1 = NormalizeOrZero(_WindDir.xz);
                float2 windDir2 = NormalizeOrZero(_WindDir2.xz);
                float windMask = saturate(uv.y);
                windMask *= windMask;

                float primaryPhase = dot(positionWS.xz, float2(0.11, 0.07));
                float secondaryPhase = dot(positionWS.xz, float2(-0.05, 0.09));
                float primaryWave = sin(primaryPhase + (_Time.y * _WindSpeed));
                float secondaryWave = sin(secondaryPhase - (_Time.y * (_WindSpeed * 1.37)) + 1.5707963);
                float2 windOffsetXZ = (windDir1 * primaryWave + windDir2 * secondaryWave * 0.5) * (_WindIntensity * windMask);

                return positionWS + float3(windOffsetXZ.x, 0.0, windOffsetXZ.y);
            }

            half4 SampleAlbedo(float2 uvBase, float2 uvDiffuse)
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvBase);
                half4 diffuseSample = SAMPLE_TEXTURE2D(_Diffuse, sampler_Diffuse, uvDiffuse);
                return lerp(baseSample, diffuseSample, saturate(_UseLegacyDiffuse));
            }

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                float3 displacedWS = ApplyWindOffset(positionInputs.positionWS, input.uv);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - displacedWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                float4 clipPos = TransformWorldToHClip(ApplyShadowBias(displacedWS, normalInputs.normalWS, lightDirectionWS));
                clipPos = ApplyShadowClamping(clipPos);

                output.positionCS = clipPos;
                output.uvBase = TRANSFORM_TEX(input.uv, _BaseMap);
                output.uvDiffuse = TRANSFORM_TEX(input.uv, _Diffuse);
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 albedoSample = SampleAlbedo(input.uvBase, input.uvDiffuse);
                half alpha = albedoSample.a * _BaseColor.a;
                if (_AlphaClip > 0.5)
                {
                    clip(alpha - _Cutoff);
                }

                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
