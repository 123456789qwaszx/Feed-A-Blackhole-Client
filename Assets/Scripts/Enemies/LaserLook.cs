using UnityEngine;

namespace BlackHole.Unity
{
    // 현재 아래 값은 모두 [임시].
    // 레이저 별(LaserBurst 사망 효과)의 외형 설정 에셋. 화면(LaserBeams)만 읽는다. 판정 수치(피해·너비)와 시간(예고 DeathEffects.LaserTelegraphSeconds)은 규칙의 것이다.
    // 연출은 세 동작으로 나뉜다 — ① 표식: 별이 터지며 X 표식이 튀어나온다(섬광 + 가로 플레어 + 빔 방향의 짧은 세로 줄기, 튕기듯 한 번 커졌다 자리 잡음).
    // ② 조준: 거의 동시에 빔 방향의 가는 실선이 중심에서 화면 밖으로 빠르게 뻗어 경로를 조준하고, 발사 직전 표식이 살짝 움츠러들며 실선이 진해진다(예비 동작).
    // ③ 관통: 실선이 판정 너비를 넘겼다가 자리 잡으며 화면을 관통하고, 표식은 파도처럼 가라앉고, 발사 섬광이 터진다.
    // 요소: 섬광(Flash) · 플레어(Flare, 빔 방향에서 FlareAngle만큼 꺾인 가로 줄기) · 표식 세로 줄기(Mark, 빔 방향의 짧은 줄기) · 선(Line) → 빔(Beam).
    // 플레어와 표식 줄기는 같은 FlareGrow 곡선으로 자라고 발사 순간 FlareCollapseSeconds 동안 가라앉는다. 빔 굵기는 BeamWidth 곡선 × 판정 너비(1 = 맞는 범위).
    // 선과 빔의 길이는 화면(카메라)의 가장 먼 모서리까지다 — 규칙의 양 끝(±300)을 그대로 그리면 중심에서 자라는 느낌이 나지 않는다.
    // 전장 배율(카메라가 넓어진 만큼)을 굵기·번짐·플레어 길이·섬광 크기에 곱해 화면에서 같은 크기로 보이게 한다(ScaleWithField). 빔의 판정 너비는 곱하지 않는다.
    [CreateAssetMenu(fileName = "LaserLook", menuName = "BlackHole/Laser Look")]
    public sealed class LaserLook : ScriptableObject
    {
        [Header("외형")]
        [Tooltip("BlackHole/Laser Beam 셰이더의 머티리얼.")]
        [SerializeField] private Material _material;
        [Tooltip("번짐의 색. 레이저 별 표식 색 계열.")]
        [SerializeField] private Color _color = new(1f, 0.35f, 0.3f, 1f);
        [Tooltip("심의 색(거의 흰색).")]
        [SerializeField] private Color _coreColor = new(1f, 0.95f, 0.9f, 1f);
        [Tooltip("번짐의 세기. 0이면 번지지 않는다.")]
        [Range(0, 1)]
        [SerializeField] private float _glowStrength = 1f;
        [Tooltip("치명타 발사일 때 빔의 번짐 폭과 섬광 크기에 곱하는 배율.")]
        [Min(1)]
        [SerializeField] private float _criticalBoost = 1.3f;
        [Tooltip("굵기·번짐·플레어 길이·섬광 크기에 전장 배율(Hq.FieldScale)을 곱한다. 판정 너비(빔 굵기)는 곱하지 않는다.")]
        [SerializeField] private bool _scaleWithField = true;

        [Header("섬광: 죽은 자리의 둥근 빛")]
        [Tooltip("예고 시작 섬광의 반지름(월드 단위).")]
        [Min(0)]
        [SerializeField] private float _flashRadius = 0.9f;
        [Tooltip("섬광 바깥으로 번지는 빛의 폭(월드 단위).")]
        [Min(0.001f)]
        [SerializeField] private float _flashGlowWidth = 1.2f;
        [Tooltip("예고 진행(0 ~ 1)에서 시작 섬광의 세기. 처음에 번쩍이고 곧 꺼져야 한다.")]
        [SerializeField]
        private AnimationCurve _flashCurve = new AnimationCurve(
            new Keyframe(0, 1, 0, -4), new Keyframe(0.45f, 0, 0, 0), new Keyframe(1, 0));
        [Tooltip("발사 섬광의 반지름 = 시작 섬광 반지름 × 이 값.")]
        [Min(0)]
        [SerializeField] private float _burstFlashScale = 1.8f;
        [Tooltip("발사 진행(0 ~ 1)에서 발사 섬광의 세기.")]
        [SerializeField]
        private AnimationCurve _burstFlashCurve = new AnimationCurve(
            new Keyframe(0, 1, 0, -3), new Keyframe(0.5f, 0, 0, 0), new Keyframe(1, 0));

        [Header("플레어: 빔 방향에서 꺾인 가로 줄기")]
        [Tooltip("빔 방향에서 꺾이는 각(도)의 범위. 레이저마다 이 안에서 뽑고 좌우는 무작위(같은 레이저는 예고와 발사가 같은 각을 쓴다).")]
        [SerializeField] private Vector2 _flareAngleDegrees = new(70, 80);
        [Tooltip("다 자랐을 때 중심에서 한쪽 끝까지의 길이(월드 단위, 전장 배율을 곱한다).")]
        [Min(0)]
        [SerializeField] private float _flareReach = 5f;
        [Tooltip("X 표식의 세로 줄기(빔 방향)가 다 자랐을 때 중심에서 한쪽 끝까지의 길이(월드 단위, 전장 배율을 곱한다). 플레어와 같은 곡선으로 자란다.")]
        [Min(0)]
        [SerializeField] private float _markReach = 3f;
        [Tooltip("플레어 가운데의 심 굵기(월드 단위). 끝으로 갈수록 뾰족해진다.")]
        [Min(0.001f)]
        [SerializeField] private float _flareThickness = 0.16f;
        [Tooltip("플레어 바깥으로 번지는 빛의 폭(월드 단위).")]
        [Min(0.001f)]
        [SerializeField] private float _flareGlowWidth = 0.6f;
        [Tooltip("예고 진행(0 ~ 1)에서 플레어·표식 길이의 비율. 튕기듯 1을 넘겼다가(표식 등장) 자리 잡고, 발사 직전 살짝 움츠러든다(예비 동작). 1을 넘어도 된다.")]
        [SerializeField]
        private AnimationCurve _flareGrow = new AnimationCurve(
            new Keyframe(0, 0, 0, 12), new Keyframe(0.12f, 1.15f, 0, 0), new Keyframe(0.25f, 1, 0, 0), new Keyframe(0.75f, 1, 0, 0), new Keyframe(1, 0.85f, 0, 0));
        [Tooltip("예고 진행(0 ~ 1)에서 플레어의 세기(0 ~ 1).")]
        [SerializeField]
        private AnimationCurve _flareFade = new AnimationCurve(
            new Keyframe(0, 0.6f, 0, 3), new Keyframe(0.2f, 1, 0, 0), new Keyframe(1, 1, 0, 0));
        [Tooltip("발사 뒤 플레어·표식이 가라앉는 시간(초). 길이가 줄고 옅어진다.")]
        [Min(0.01f)]
        [SerializeField] private float _flareCollapseSeconds = 0.12f;

        [Header("선: 예고 동안 빔 방향으로 자라는 실선")]
        [Tooltip("실선의 굵기(월드 단위, 전장 배율을 곱한다).")]
        [Min(0.001f)]
        [SerializeField] private float _lineThickness = 0.05f;
        [Tooltip("실선 바깥으로 번지는 빛의 폭(월드 단위).")]
        [Min(0.001f)]
        [SerializeField] private float _lineGlowWidth = 0.15f;
        [Tooltip("예고 진행(0 ~ 1)에서 실선 길이의 비율(0 = 중심, 1 = 화면 밖). 표식과 거의 동시에 출발해 빠르게 뻗는다(조준).")]
        [SerializeField]
        private AnimationCurve _lineGrow = new AnimationCurve(
            new Keyframe(0.06f, 0, 0, 0), new Keyframe(0.4f, 1, 4, 0), new Keyframe(1, 1, 0, 0));
        [Tooltip("예고 진행(0 ~ 1)에서 실선의 세기(0 ~ 1). 조준 중엔 옅게 머물다 발사 직전 빠르게 진해진다.")]
        [SerializeField]
        private AnimationCurve _lineFade = new AnimationCurve(
            new Keyframe(0, 0.3f, 0, 0), new Keyframe(0.78f, 0.45f, 0, 0), new Keyframe(1, 1, 5, 0));

        [Header("빔: 발사. 가로는 발사 뒤 시간의 진행(0 ~ 1, BurstSeconds)")]
        [Tooltip("발사 연출이 보이는 시간(초).")]
        [Min(0.01f)]
        [SerializeField] private float _burstSeconds = 0.32f;
        [Tooltip("빔 굵기 = 판정 너비 × 이 값. 순식간에 1을 넘겼다가(관통) 1로 가라앉는다. 보이는 굵기 = 맞는 범위가 되려면 1 근처에 머물러야 한다.")]
        [SerializeField]
        private AnimationCurve _beamWidth = new AnimationCurve(
            new Keyframe(0, 0.05f, 0, 40), new Keyframe(0.08f, 1.4f, 0, 0), new Keyframe(0.25f, 1, 0, 0), new Keyframe(1, 0.8f, 0, 0));
        [Tooltip("빔 바깥으로 번지는 빛의 폭(월드 단위, 전장 배율을 곱한다).")]
        [Min(0.001f)]
        [SerializeField] private float _beamGlowWidth = 0.8f;
        [Tooltip("발사 진행(0 ~ 1)에서 빔의 세기(0 ~ 1). 끝에서 0이어야 한다.")]
        [SerializeField]
        private AnimationCurve _beamFade = new AnimationCurve(
            new Keyframe(0, 1, 0, 0), new Keyframe(0.4f, 1, 0, 0), new Keyframe(1, 0, -2, 0));

        [Header("성능")]
        [Tooltip("동시에 보이는 발사의 상한. 넘치면 가장 오래된 발사를 다시 쓴다. 예고는 규칙이 주는 만큼 그린다.")]
        [Min(1)]
        [SerializeField] private int _maxBursts = 8;

        public Material Material => _material;
        public Color Color => _color;
        public Color CoreColor => _coreColor;
        public float GlowStrength => _glowStrength;
        public float CriticalBoost => _criticalBoost;
        public bool ScaleWithField => _scaleWithField;
        public float FlashRadius => _flashRadius;
        public float FlashGlowWidth => _flashGlowWidth;
        public float BurstFlashScale => _burstFlashScale;
        public Vector2 FlareAngleDegrees => _flareAngleDegrees;
        public float FlareReach => _flareReach;
        public float MarkReach => _markReach;
        public float FlareThickness => _flareThickness;
        public float FlareGlowWidth => _flareGlowWidth;
        public float FlareCollapseSeconds => _flareCollapseSeconds;
        public float LineThickness => _lineThickness;
        public float LineGlowWidth => _lineGlowWidth;
        public float BurstSeconds => _burstSeconds;
        public float BeamGlowWidth => _beamGlowWidth;
        public int MaxBursts => _maxBursts;

        public float Flash(float progress) => Evaluate(_flashCurve, progress, 0);
        public float BurstFlash(float progress) => Evaluate(_burstFlashCurve, progress, 0);
        // 플레어·표식 길이의 비율. 튕김을 위해 1을 넘을 수 있다.
        public float FlareGrowth(float progress) => _flareGrow == null ? 1 : Mathf.Max(0, _flareGrow.Evaluate(Mathf.Clamp01(progress)));
        public float FlareStrength(float progress) => Evaluate(_flareFade, progress, 1);
        public float LineGrowth(float progress) => Evaluate(_lineGrow, progress, 1);
        public float LineStrength(float progress) => Evaluate(_lineFade, progress, 1);
        public float BeamStrength(float progress) => Evaluate(_beamFade, progress, 1 - progress);
        // 판정 너비에 곱하는 배율. 곡선이 없으면 1.
        public float BeamWidthScale(float progress) => _beamWidth == null ? 1 : Mathf.Max(0, _beamWidth.Evaluate(Mathf.Clamp01(progress)));

        private static float Evaluate(AnimationCurve curve, float progress, float fallback) =>
            curve == null ? fallback : Mathf.Clamp01(curve.Evaluate(Mathf.Clamp01(progress)));
    }
}
