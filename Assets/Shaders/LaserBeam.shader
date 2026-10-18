// 레이저 별의 빛줄기 한 토막: 중심에서 양쪽으로 _Length만큼 뻗는 곧은 선. 스프라이트 없이 "선분까지의 거리"로 그린다(Lightning Bolt와 같은 방식).
// - Core: 선분에서 굵기 _Thickness 안의 밝은 심. Glow: 심 밖으로 옅어지는 번짐. 양 끝은 둥글다.
// - _Taper(0 ~ 1): 끝으로 갈수록 가늘고 어두워지는 정도. 0이면 끝까지 같은 굵기(빔), 1이면 끝이 뾰족한 섬광 줄기(플레어).
// - _Length = 0이면 선분이 점이 되어 둥근 섬광(원형 빛)이 된다.
// 사각형 메시(quad)를 선 방향으로 돌려 놓고 그린다. 길이 파라미터는 모두 월드 단위다.
// 알파 혼합: 심을 번짐 위에 덮어(over) 화면 위에 그린다(Lightning Bolt와 같은 혼합). 알파(_Fade)는 전체 세기로 쓴다.
Shader "BlackHole/Laser Beam"
{
    Properties
    {
        _Length ("Length", Float) = 1
        _Thickness ("Thickness", Float) = 0.1
        _Color ("Color", Color) = (1, 0.35, 0.3, 1)
        _CoreColor ("Core Color", Color) = (1, 0.95, 0.9, 1)
        _GlowWidth ("Glow Width", Float) = 0.3
        _GlowStrength ("Glow Strength", Range(0, 1)) = 1
        _Taper ("Taper", Range(0, 1)) = 0
        _Fade ("Fade", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        // LightMode 태그가 없는 패스(SRPDefaultUnlit)는 2D 렌더러와 기본 렌더러 모두 그린다.
        Pass
        {
            Name "Unlit"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Length;
                float _Thickness;
                half4 _Color;
                half4 _CoreColor;
                float _GlowWidth;
                half _GlowStrength;
                half _Taper;
                half _Fade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                // 선 중심에서 이 점까지를 선 방향(x)과 그 수직(y)으로 잰 월드 거리.
                float2 local : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 centerWS = TransformObjectToWorld(float3(0, 0, 0));
                float2 along = normalize(TransformObjectToWorldDir(float3(1, 0, 0)).xy);
                float2 offset = positionWS.xy - centerWS.xy;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.local = float2(dot(offset, along), dot(offset, float2(-along.y, along.x)));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float halfLength = max(_Length * 0.5, 0);
                // 끝 너머는 끝점까지의 거리로 잰다(둥근 끝).
                float x = clamp(input.local.x, -halfLength, halfLength);
                float gap = length(float2(input.local.x - x, input.local.y));

                // 끝으로 갈수록(u → 1) 가늘고 어두워진다. _Taper가 0이면 바뀌지 않는다.
                float u = halfLength > 1e-4 ? saturate(abs(x) / halfLength) : 0;
                float shape = 1 - u;
                float thin = lerp(1, shape, _Taper);
                float dim = lerp(1, shape * shape, _Taper);
                float halfThickness = _Thickness * 0.5 * thin;
                float glowWidth = max(_GlowWidth * lerp(1, 0.35 + 0.65 * shape, _Taper), 1e-4);

                // 픽셀 하나의 월드 폭. 줌·해상도와 무관하게 가장자리를 한 픽셀로 부드럽게 한다.
                float aa = max(fwidth(gap), 1e-5);
                float core = 1 - smoothstep(halfThickness - aa, halfThickness + aa, gap);
                float glow = exp(-max(gap - halfThickness, 0) / glowWidth) * _GlowStrength;

                half glowAmount = saturate(glow) * _Color.a * dim;
                half coreAmount = core * _CoreColor.a * dim;
                // 심을 번짐 위에 덮는다(over). 색은 덮은 양으로 나눠 알파와 따로 둔다.
                half alpha = coreAmount + glowAmount * (1 - coreAmount);
                half3 color = (_CoreColor.rgb * coreAmount + _Color.rgb * glowAmount * (1 - coreAmount)) / max(alpha, 1e-4);

                // Blend SrcAlpha OneMinusSrcAlpha: 화면 = lerp(화면, color, alpha × _Fade).
                return half4(color, alpha * _Fade);
            }
            ENDHLSL
        }
    }
}
