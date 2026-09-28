using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 사망 효과의 화면. 매 프레임 판의 효과 기록을 읽어 번개 선과 폭발 원을 잠깐 그린다.
    // 게임 상태를 바꾸지 않고, 피해를 다시 계산하지 않는다(SYSTEM_CATALOG S06 → S09).
    // 정지 중에는 판이 기록을 비우지 않으므로, 이미 그린 기록은 번호로 걸러 두 번 그리지 않는다.
    internal sealed class DeathEffectView : IDisposable
    {
        private const float LightningWidth = 0.08f;
        private const float LightningSeconds = 0.25f;
        private const float ExplosionWidth = 0.12f;
        private const float ExplosionSeconds = 0.4f;
        private static readonly Color LightningColor = new Color(0.55f, 0.8f, 1f, 1f);
        private static readonly Color ExplosionColor = new Color(1f, 0.45f, 0.15f, 1f);

        private readonly LineStrokes _strokes;
        // 그린 기록의 가장 큰 번호. 번개와 폭발은 같은 번호 줄을 쓴다.
        private long _drawn;

        public DeathEffectView(Transform parent) => _strokes = new LineStrokes(parent, "Death Effect View");

        public void Synchronize(World world, float delta)
        {
            _strokes.Age(delta);
            long drawn = _drawn;
            IReadOnlyList<LightningHit> hits = world.DeathEffects.LightningHits;

            for (int i = 0; i < hits.Count; i++)
            {
                if (hits[i].Sequence <= _drawn)
                    continue;

                drawn = Math.Max(drawn, hits[i].Sequence);
                LineStrokes.SetSegment(_strokes.Flash("Lightning", LightningWidth, LightningColor, LightningSeconds), hits[i].From, hits[i].To);
            }

            IReadOnlyList<ExplosionBlast> explosions = world.DeathEffects.Explosions;

            for (int i = 0; i < explosions.Count; i++)
            {
                if (explosions[i].Sequence <= _drawn)
                    continue;

                drawn = Math.Max(drawn, explosions[i].Sequence);
                LineStrokes.SetCircle(_strokes.Flash("Explosion", ExplosionWidth, ExplosionColor, ExplosionSeconds),
                    explosions[i].Center, explosions[i].Radius);
            }

            _drawn = drawn;
        }

        // 그리는 선이 없고, 지운 객체도 장면에서 모두 사라졌는가(Reset 뒤 한 프레임).
        public bool IsClear => _strokes.IsClear;

        // 판이 바뀌거나 판을 정리할 때 모든 선을 지운다. 새 판의 기록 번호는 1부터다.
        public void Reset()
        {
            _strokes.Reset();
            _drawn = 0;
        }

        public void Dispose() => _strokes.Dispose();
    }
}
