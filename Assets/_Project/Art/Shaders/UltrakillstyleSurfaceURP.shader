Shader "Ultrakillstyle/SurfaceURP"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1,1,1,1)
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,1)
        _EmissionMap ("Emission Map", 2D) = "white" {}
        _SnapResolution ("Vertex Snap Grid", Range(32, 640)) = 160
        _LightBands ("Light Bands", Range(2, 8)) = 4
        _AmbientBoost ("Ambient Boost", Range(0, 2)) = 1
        _ShadowFloor ("Shadow Floor", Range(0, 0.5)) = 0.08
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _EmissionColor;
                float _SnapResolution;
                float _LightBands;
                float _AmbientBoost;
                float _ShadowFloor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float4 clip = TransformWorldToHClip(positionWS);
                float2 ndc = clip.xy / clip.w;
                ndc = floor(ndc * _SnapResolution + 0.5) / _SnapResolution;
                clip.xy = ndc * clip.w;
                o.positionCS = clip;
                o.positionWS = positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(clip.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                float3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv).rgb * _EmissionColor.rgb;

                float3 n = normalize(i.normalWS);
                float bands = max(2.0, _LightBands);

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float lit = saturate(dot(n, mainLight.direction)) * mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                lit = floor(lit * bands + 0.5) / bands;
                float3 light = mainLight.color * lit;

                #ifdef _ADDITIONAL_LIGHTS
                uint count = GetAdditionalLightsCount();
                for (uint li = 0u; li < count; li++)
                {
                    Light l = GetAdditionalLight(li, i.positionWS);
                    float a = saturate(dot(n, l.direction)) * l.distanceAttenuation * l.shadowAttenuation;
                    a = floor(a * bands + 0.5) / bands;
                    light += l.color * a;
                }
                #endif

                float3 ambient = SampleSH(n) * _AmbientBoost;
                light = max(ambient + light, _ShadowFloor.xxx);

                float3 col = albedo.rgb * light + emission;
                col = MixFog(col, i.fogFactor);
                return half4(col, 1.0);
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

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowVert(Attributes v)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                o.positionCS = positionCS;
                return o;
            }

            half4 ShadowFrag(Varyings i) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthVert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half4 DepthFrag(Varyings i) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
