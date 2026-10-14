// 적 몸체(SpriteRenderer)용 음영 셰이더: 남은 체력 비율만큼의 면적에 음영을 씌우고, 피해를 입은 만큼 기본색이 드러난다.
// - 음영 = 색 × (1 − _ShadeStrength). 0.3이면 '알파 30% 검정을 위에 한 겹 덮은 것'과 수학적으로 같은 색이다
//   (검정 α0.3 over C = 0.7·C). 겹 스프라이트·마스크 없이 렌더러 하나로 끝나서 적 수만큼 오브젝트가 늘지 않는다.
// - 경계선: 스프라이트 중심(_ShadeCenter)에서 로컬 위쪽(+y)으로 잰 거리 ÷ 반지름(_ShadeRadius) = s(−1 ~ 1). 모두 오브젝트 공간이다.
//   s > _ShadeThreshold 인 쪽이 기본색, 나머지가 음영. 문턱값은 '원의 면적 비율'이 피해 비율과 같아지도록 C#이 계산해 넣는다
//   (EnemyView.ShadeThresholdOf). 오브젝트 공간이라 자전·피격 흔들림·크기 변화를 따라 경계선이 저절로 돈다 —
//   C#은 중심·반지름을 출현 때 한 번, 문턱값을 체력이 바뀔 때만 넣으면 된다(매 프레임 갱신 없음).
//   전제: 렌더러에 프로퍼티 블록이 붙어 있어 정점이 미리 월드로 합쳐지는 배칭에서 빠진다(EnemyView가 출현 때 붙인다).
// - _ShadeSoftness: 경계의 부드러움(s 단위, 반지름 대비 비율).
// 색(정점 색 = SpriteRenderer.color, 붙잡힘 색 변화 포함)과 알파는 Sprites/Default와 같이 그대로 쓴다.
// 장면의 2D 전역 광원이 흰색·세기 1이라 Sprite-Lit-Default와 보이는 색이 같다(조명 없는 Unlit 패스).
Shader "BlackHole/Enemy Shade"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _ShadeStrength ("Shade Strength (검정 덮개의 알파)", Range(0, 1)) = 0.3
        _ShadeSoftness ("Shade Edge Softness", Range(0.001, 0.5)) = 0.05
        [PerRendererData] _ShadeCenter ("Center (object xy)", Vector) = (0, 0, 0, 0)
        [PerRendererData] _ShadeRadius ("Radius (object)", Float) = 0.5
        [PerRendererData] _ShadeThreshold ("Threshold", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _ShadeStrength;
                half _ShadeSoftness;
                float4 _ShadeCenter;
                float _ShadeRadius;
                float _ShadeThreshold;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 positionOS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xy;
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;

                float s = (input.positionOS.y - _ShadeCenter.y) / max(_ShadeRadius, 1e-4);
                half lit = smoothstep(_ShadeThreshold - _ShadeSoftness, _ShadeThreshold + _ShadeSoftness, s);
                color.rgb *= lerp(1 - _ShadeStrength, 1, lit);
                return color;
            }
            ENDHLSL
        }
    }
}
