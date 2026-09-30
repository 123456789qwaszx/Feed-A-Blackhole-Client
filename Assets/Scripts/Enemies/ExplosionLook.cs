using UnityEngine;

namespace BlackHole.Unity
{
    // 현재 아래 값은 모두 [임시].
    // 폭발(초신성 등 폭발 사망 효과)의 외형 설정 에셋. 화면(ExplosionRings)만 읽는다. 판정 수치(피해·반지름)는 적 종류 에셋에 있다.
    // 링이 가장 크게 퍼졌을 때의 반지름은 판정 반지름 × RadiusScale이다. 1이면 보이는 범위와 맞는 범위가 같다.
    [CreateAssetMenu(fileName = "ExplosionLook", menuName = "BlackHole/Explosion Look")]
    public sealed class ExplosionLook : ScriptableObject
    {
        [Header("외형")]
        [Tooltip("BlackHole/Explosion Ring 셰이더의 머티리얼.")]
        [SerializeField] private Material _material;
        [Tooltip("링 굵기(월드 단위). 반지름이 바뀌어도 그대로다.")]
        [Min(0.001f)]
        [SerializeField] private float _thickness = 0.15f;
        [SerializeField] private Color _color = new(1f, 0.55f, 0.2f, 1f);
        [Tooltip("링 바깥으로 번지는 빛의 폭(월드 단위). 이만큼 멀어지면 약 37%로 옅어진다.")]
        [Min(0.001f)]
        [SerializeField] private float _glowWidth = 0.3f;
        [Tooltip("번짐의 세기. 0이면 번지지 않는다.")]
        [Range(0, 1)]
        [SerializeField] private float _glowStrength = 0.5f;
        [Tooltip("링 안쪽을 채우는 섬광의 색.")]
        [SerializeField] private Color _flashColor = new(1f, 0.95f, 0.8f, 1f);

        [Header("움직임: 가로는 진행(0 ~ 1)")]
        [Tooltip("폭발 한 번의 시간(초).")]
        [Min(0.01f)]
        [SerializeField] private float _duration = 0.5f;
        [Tooltip("가장 크게 퍼졌을 때의 반지름 = 판정 반지름 × 이 값.")]
        [Min(0)]
        [SerializeField] private float _radiusScale = 1f;
        [Tooltip("링의 퍼짐. 세로는 가장 큰 반지름에 대한 비율(0 ~ 1). 처음에 빠르고 끝에 느리면 충격파처럼 보인다.")]
        [SerializeField]
        private AnimationCurve _expandCurve = new AnimationCurve(
            new Keyframe(0, 0, 0, 3), new Keyframe(1, 1, 0, 0));
        [Tooltip("안쪽 섬광의 세기(0 ~ 1). 처음에 번쩍이고 곧 꺼져야 한다.")]
        [SerializeField]
        private AnimationCurve _flashCurve = new AnimationCurve(
            new Keyframe(0, 0.9f), new Keyframe(0.3f, 0), new Keyframe(1, 0));
        [Tooltip("전체 세기(0 ~ 1). 끝에서 0이어야 한다.")]
        [SerializeField]
        private AnimationCurve _fadeCurve = new AnimationCurve(
            new Keyframe(0, 1), new Keyframe(1, 0));

        [Header("성능")]
        [Tooltip("동시에 보이는 폭발의 상한. 넘치면 가장 오래된 폭발을 다시 쓴다.")]
        [Min(1)]
        [SerializeField] private int _maxBlasts = 16;

        public Material Material => _material;
        public float Thickness => _thickness;
        public Color Color => _color;
        public float GlowWidth => _glowWidth;
        public float GlowStrength => _glowStrength;
        public Color FlashColor => _flashColor;
        public float Duration => _duration;
        public float RadiusScale => _radiusScale;
        public int MaxBlasts => _maxBlasts;

        // 진행(0 ~ 1)에서의 값. 곡선이 없으면 퍼짐은 진행 그대로, 섬광은 없음, 세기는 일정하게 줄어든다.
        public float Expand(float progress) => _expandCurve == null ? progress : _expandCurve.Evaluate(progress);
        public float Flash(float progress) => _flashCurve == null ? 0 : Mathf.Clamp01(_flashCurve.Evaluate(progress));
        public float Fade(float progress) => _fadeCurve == null ? 1 - progress : Mathf.Clamp01(_fadeCurve.Evaluate(progress));
    }
}
