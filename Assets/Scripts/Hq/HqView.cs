using System;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 블랙홀(HQ)의 화면. 매 프레임 판의 블랙홀을 읽어 원점에 원을 그린다. 게임 상태를 바꾸지 않는다.
    // 원의 크기는 성장도가 정하고(판 동안 같다), 판 Level이 오른 순간 바깥으로 한 번 번쩍인다. 그림일 뿐이다 — 출현 띠·공전·Breaker와 무관하다(GAME_RULES 3.1).
    // 원작의 "블랙홀이 한 단계 커진다"를 성장도로 읽었다 [임시, BATTLE_COMPOSITION_PLAN 8.2].
    // 크기는 [임시]다: 마지막 성장도(30)에서도 가장 작은 출현 거리(2)보다 작게 둔다.
    internal sealed class HqView : IDisposable
    {
        private const float BaseRadius = 0.35f;
        private const float RadiusPerStage = 0.05f;
        private const float RingWidth = 0.08f;
        private const float FlashWidth = 0.12f;
        private const float FlashSeconds = 0.5f;
        private static readonly Color RingColor = new Color(0.72f, 0.62f, 1f, 0.9f);
        private static readonly Color FlashColor = new Color(0.9f, 0.85f, 1f, 1f);

        private readonly LineStrokes _strokes;
        private LineRenderer _ring;
        private int _shownLevel = -1;

        public HqView(Transform parent) => _strokes = new LineStrokes(parent, "Hq View");

        public void Synchronize(World world, float delta)
        {
            _strokes.Age(delta);

            if (_ring == null)
                _ring = _strokes.Line("Black Hole", RingWidth, RingColor);

            int level = world.Hq.Level;

            if (level == _shownLevel)
                return;

            float radius = RadiusOf(world.Hq.Stage);
            LineStrokes.SetCircle(_ring, BattleSpace.Origin, radius);

            if (_shownLevel >= 0 && level > _shownLevel)
                LineStrokes.SetCircle(_strokes.Flash("Level Up", FlashWidth, FlashColor, FlashSeconds), BattleSpace.Origin, radius * 1.4f);

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
        }

        public void Dispose() => _strokes.Dispose();

        private static float RadiusOf(int stage) => BaseRadius + RadiusPerStage * (stage - HqGrowthDefinition.StartStage);
    }
}
