Shader "Ultrakillstyle/UIDither"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _TargetHeight ("Target Height", Range(120, 1080)) = 270
        _Fade ("Fade", Range(0, 1)) = 1
        _EdgeFadeX ("Edge Fade X", Range(0, 0.5)) = 0.25
        _EdgeFadeY ("Edge Fade Y", Range(0, 0.5)) = 0
        _ColorSteps ("Color Steps", Range(2, 64)) = 24
        _ScanlineStrength ("Scanline Strength", Range(0, 1)) = 0.15

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 screen : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _TargetHeight;
            float _Fade;
            float _EdgeFadeX;
            float _EdgeFadeY;
            float _ColorSteps;
            float _ScanlineStrength;

            float bayer4(int2 p)
            {
                p = p & 3;
                int v = ((p.x ^ p.y) & 1) * 8 + (p.y & 1) * 4 + (((p.x ^ p.y) >> 1) & 1) * 2 + ((p.y >> 1) & 1);
                return (v + 0.5) / 16.0;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.uv = v.uv;
                o.screen = ComputeScreenPos(o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;

                float2 screenPx = (i.screen.xy / i.screen.w) * _ScreenParams.xy;
                float cell = max(1.0, _ScreenParams.y / _TargetHeight);
                int2 lowPx = int2(floor(screenPx / cell));

                float ex = _EdgeFadeX > 0.0 ? saturate(min(i.uv.x, 1.0 - i.uv.x) / _EdgeFadeX) : 1.0;
                float ey = _EdgeFadeY > 0.0 ? saturate(min(i.uv.y, 1.0 - i.uv.y) / _EdgeFadeY) : 1.0;
                float alpha = col.a * ex * ey * _Fade;

                clip(alpha - bayer4(lowPx) - 0.001);

                float steps = max(2.0, _ColorSteps);
                col.rgb = floor(col.rgb * (steps - 1.0) + 0.5) / (steps - 1.0);
                col.rgb *= 1.0 - _ScanlineStrength * (lowPx.y & 1);
                col.a = 1.0;
                return col;
            }
            ENDCG
        }
    }
}
