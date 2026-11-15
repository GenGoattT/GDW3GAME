Shader "Ultrakillstyle/FullScreenURP"
{
    Properties
    {
        _TargetHeight ("Target Height", Range(120, 1080)) = 270
        _ColorSteps ("Color Steps", Range(2, 64)) = 24
        _DitherStrength ("Dither Strength", Range(0, 2)) = 1
        _Saturation ("Saturation", Range(0, 2)) = 1.25
        _Contrast ("Contrast", Range(0.5, 2)) = 1.15
        _Brightness ("Brightness", Range(0.5, 2)) = 1.05
        _VignetteStrength ("Vignette Strength", Range(0, 3)) = 0.9
        _ShadowTint ("Shadow Tint", Color) = (0.85, 0.88, 1, 1)
        _HighlightTint ("Highlight Tint", Color) = (1, 0.97, 0.9, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "Ultrakillstyle"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _TargetHeight;
            float _ColorSteps;
            float _DitherStrength;
            float _Saturation;
            float _Contrast;
            float _Brightness;
            float _VignetteStrength;
            float4 _ShadowTint;
            float4 _HighlightTint;

            float bayer8(int2 p)
            {
                p = p & 7;
                int a = p.x ^ p.y;
                int v = ((p.y & 1) << 5) | ((a & 1) << 4) | ((p.y & 2) << 2) | ((a & 2) << 1) | ((p.y & 4) >> 1) | ((a & 4) >> 2);
                return v / 64.0;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 lowRes = float2(max(1.0, round(_TargetHeight * aspect)), _TargetHeight);
                float2 px = floor(uv * lowRes);
                float2 suv = (px + 0.5) / lowRes;

                float3 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, suv).rgb;

                col *= _Brightness;
                col = (col - 0.5) * _Contrast + 0.5;

                float luma = dot(col, float3(0.299, 0.587, 0.114));
                col = lerp(luma.xxx, col, _Saturation);
                col *= lerp(_ShadowTint.rgb, _HighlightTint.rgb, saturate(luma));

                float steps = max(2.0, _ColorSteps);
                float threshold = bayer8(int2(px)) - 0.5 + (0.5 / 64.0);
                col += threshold * _DitherStrength / (steps - 1.0);

                col = saturate(col);
                col = floor(col * (steps - 1.0) + 0.5) / (steps - 1.0);

                float2 d = uv - 0.5;
                float vig = 1.0 - dot(d, d) * _VignetteStrength;
                col *= saturate(vig);

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
