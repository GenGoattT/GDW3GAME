Shader "Ultrakillstyle/SnowSurfaceURP"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1,1,1,1)
        _SnapResolution ("Vertex Snap Grid (0 = off)", Range(0, 640)) = 0
        _LightBands ("Light Bands", Range(2, 8)) = 4
        _AmbientBoost ("Ambient Boost", Range(0, 2)) = 1
        _ShadowFloor ("Shadow Floor", Range(0, 0.5)) = 0.08

        [Header(Snow)]
        _SnowColor ("Snow Color", Color) = (0.86, 0.9, 0.97, 1)
        _SnowShadowTint ("Snow Shadow Tint", Color) = (0.62, 0.72, 0.92, 1)
        _SnowCoverage ("Coverage On Flat Ground", Range(0, 1.5)) = 0.9
        _SnowSlopeStart ("Slope Where Snow Starts", Range(-1, 1)) = 0.35
        _SnowBreakup ("Patchiness", Range(0, 2)) = 0.7
        _SnowNoiseScale ("Patch Size", Range(0.01, 2)) = 0.18
        _SnowDitherHeight ("Dither Grid Height", Range(120, 1080)) = 270
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float _SnapResolution;
            float _LightBands;
            float _AmbientBoost;
            float _ShadowFloor;
            float4 _SnowColor;
            float4 _SnowShadowTint;
            float _SnowCoverage;
            float _SnowSlopeStart;
            float _SnowBreakup;
            float _SnowNoiseScale;
            float _SnowDitherHeight;
        CBUFFER_END

        float4 SnapClip(float4 clip)
        {
            if (_SnapResolution < 1.0) return clip;
            float2 ndc = clip.xy / clip.w;
            ndc = floor(ndc * _SnapResolution + 0.5) / _SnapResolution;
            clip.xy = ndc * clip.w;
            return clip;
        }
        ENDHLSL

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
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

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

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Bayer4(uint2 p)
            {
                p &= 3u;
                uint x = p.x ^ p.y;
                uint v = ((x & 1u) << 3) | ((p.y & 1u) << 2) | (((x >> 1) & 1u) << 1) | ((p.y >> 1) & 1u);
                return (v + 0.5) / 16.0;
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float4 clip = SnapClip(TransformWorldToHClip(positionWS));
                o.positionCS = clip;
                o.positionWS = positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(clip.z);
                return o;
            }

            float SnowMask(float3 n, float3 positionWS, float4 positionCS)
            {
                float slope = saturate((n.y - _SnowSlopeStart) / max(0.01, 1.0 - _SnowSlopeStart));
                float2 p = positionWS.xz * _SnowNoiseScale + positionWS.y * 0.07;
                float patches = ValueNoise(p) * 0.65 + ValueNoise(p * 2.7 + 11.3) * 0.35;
                float cover = slope * _SnowCoverage + (patches - 0.5) * _SnowBreakup * slope;

                float cell = max(1.0, _ScreenParams.y / _SnowDitherHeight);
                uint2 lowPx = (uint2)floor(positionCS.xy / cell);
                return step(Bayer4(lowPx), saturate(cover));
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 albedo = (SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor).rgb;
                float snow = SnowMask(n, i.positionWS, i.positionCS);
                albedo = lerp(albedo, _SnowColor.rgb, snow);

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
                ambient *= lerp(float3(1, 1, 1), _SnowShadowTint.rgb, snow);

                #if defined(_SCREEN_SPACE_OCCLUSION)
                AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.positionCS));
                ambient *= ao.indirectAmbientOcclusion;
                #endif

                light = max(ambient + light, _ShadowFloor.xxx);

                float3 col = albedo * light;
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
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

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
                o.positionCS = SnapClip(TransformObjectToHClip(v.positionOS.xyz));
                return o;
            }

            half4 DepthFrag(Varyings i) : SV_Target
            {
                return i.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }

            ZWrite On

            HLSLPROGRAM
            #pragma vertex NormalsVert
            #pragma fragment NormalsFrag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings NormalsVert(Attributes v)
            {
                Varyings o;
                o.positionCS = SnapClip(TransformObjectToHClip(v.positionOS.xyz));
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 NormalsFrag(Varyings i) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(normalize(i.normalWS)), 0.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
