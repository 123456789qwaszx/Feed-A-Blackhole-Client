using System;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 블랙홀(HQ)의 화면. 매 프레임 판의 블랙홀을 읽어 원점에 원을 그린다. 게임 상태를 바꾸지 않는다.
    // 원의 크기는 판 Level이 정하고, Level이 오른 순간 커지며 바깥으로 한 번 번쩍인다. 그림일 뿐이다 — 출현 띠·공전·Breaker와 무관하다(GAME_RULES 3.1).
    // 크기는 원작 실측(검은 중심의 C0 화면 지름)을 Level에 맞춘 것이다: Level 0 → 12px, 10 → 47px, 20 → 86px, 30 → 158px, 35 → 232px,
    // 40 → 약 292px(그 구간 카메라 배율이 추정값이라 오차 약 ±12%).
    // 1 unit = C0 54px(카메라 크기 10)로 바꾼 반지름을 Level 사이에서 직선으로 잇고, 마지막 점 뒤는 그대로 둔다.
    // 선 굵기는 화면에서 고정이라 판의 전장 배율을 곱한다(원은 월드 크기라 카메라가 넓어지면 작아 보인다).
    internal sealed class HqView : IDisposable
    {
        // (Level, 반지름) 실측점. Level이 커지는 순서.
        private static readonly (int Level, float Radius)[] _radiusPoints =
        {
            (0, 0.111f),
            (10, 0.435f),
            (20, 0.796f),
            (30, 1.463f),
            (35, 2.148f),
            (40, 2.704f),
        };

        private const float RingWidth = 0.08f;
        private const float FlashWidth = 0.12f;
        private const float FlashSeconds = 0.5f;
        private static readonly Color RingColor = new Color(0.72f, 0.62f, 1f, 0.9f);
        private static readonly Color FlashColor = new Color(0.9f, 0.85f, 1f, 1f);

        private readonly LineStrokes _strokes;
        private readonly GameObject _blackHole;
        private LineRenderer _ring;
        private int _shownLevel = -1;

        //public HqView(Transform parent) => _strokes = new LineStrokes(parent, "Hq View");
        public HqView(Transform parent, GameObject blackHole)
        {
            _strokes = new LineStrokes(parent, "Hq View");
            _blackHole = blackHole != null ? blackHole : throw new ArgumentNullException(nameof(blackHole));
            _blackHole.SetActive(false);
        }

        public void Synchronize(World world, float delta)
        {
            _strokes.Age(delta);

            float fieldScale = world.Hq.FieldScale;

            // 판이 바뀌면 Reset이 선을 지우므로, 새 판의 전장 배율로 다시 만든다.
            if (_ring == null)
                _ring = _strokes.Line("Black Hole", RingWidth * fieldScale, RingColor);

            if (!_blackHole.activeSelf)
                _blackHole.SetActive(true);

            int level = world.Hq.Level;

            if (level == _shownLevel)
                return;

            float radius = RadiusOf(level);
            LineStrokes.SetCircle(_ring, BattleSpace.Origin, radius);

            if (_shownLevel >= 0 && level > _shownLevel)
                LineStrokes.SetCircle(_strokes.Flash("Level Up", FlashWidth * fieldScale, FlashColor, FlashSeconds), BattleSpace.Origin, radius * 1.4f);

            _shownLevel = level;
        }

        // 그리는 선이 없고, 지운 객체도 장면에서 모두 사라졌는가(Reset 뒤 한 프레임).
        public bool IsClear => _strokes.IsClear;

        // 판이 바뀌거나 판을 정리할 때 지운다. 다음 판의 블랙홀은 Level 0에서 다시 그린다.
        public void Reset()
        {
            _strokes.Reset();
            _ring = null;
            _shownLevel = -1;
            _blackHole.SetActive(false);
        }

        //public void Dispose() => _strokes.Dispose();
        public void Dispose()
        {
            _strokes.Dispose();
            if (_blackHole != null)          // 추가: 씬 오브젝트라 파괴하지 않고 끄기만 한다.
                _blackHole.SetActive(false);
        }

        // Level의 반지름: 실측점 사이는 직선, 첫 점 앞·마지막 점 뒤는 끝 점의 값.
        private static float RadiusOf(int level)
        {
            if (level <= _radiusPoints[0].Level)
                return _radiusPoints[0].Radius;

            for (int i = 1; i < _radiusPoints.Length; i++)
            {
                (int toLevel, float toRadius) = _radiusPoints[i];

                if (level > toLevel)
                    continue;

                (int fromLevel, float fromRadius) = _radiusPoints[i - 1];
                return fromRadius + (toRadius - fromRadius) * (level - fromLevel) / (toLevel - fromLevel);
            }

            return _radiusPoints[_radiusPoints.Length - 1].Radius;
        }
    }
}
