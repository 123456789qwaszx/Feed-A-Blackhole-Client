using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 사망 효과의 화면. 매 프레임 판의 효과 기록을 읽어 번개와 폭발을 그린다.
    // 게임 상태를 바꾸지 않고, 피해를 다시 계산하지 않는다(SYSTEM_CATALOG S06 → S09).
    // - 번개: 옮겨 간 기록마다 선이 잠깐 보였다가 옅어진다(LineStrokes).
    // - 폭발: 기록마다 충격파 링이 퍼지며 옅어진다(ExplosionRings, 외형은 ExplosionLook).
    // 정지 중에는 판이 기록을 비우지 않으므로, 이미 그린 기록은 번호로 걸러 두 번 그리지 않는다.
    // 일시정지 중에는 연출의 시간도 멈추고, 재개하면 이어간다.
    internal sealed class DeathEffectView : IDisposable
    {
        private const float LightningWidth = 0.08f;
        private const float LightningSeconds = 0.25f;
        private static readonly Color LightningColor = new Color(0.55f, 0.8f, 1f, 1f);

        private readonly LineStrokes _strokes;
        private readonly ExplosionRings _explosions;
        // 그린 기록의 가장 큰 번호. 번개와 폭발은 같은 번호 줄을 쓴다.
        private long _drawn;

        public DeathEffectView(Transform parent, ExplosionLook explosionLook)
        {
            _strokes = new LineStrokes(parent, "Death Effect View");
            _explosions = new ExplosionRings(parent, explosionLook);
        }

        public void Synchronize(World world, bool paused, float delta)
        {
            if (!paused)
            {
                _strokes.Age(delta);
                _explosions.Age(delta);
            }

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
                _explosions.Play(explosions[i].Center, explosions[i].Radius);
            }

            _drawn = drawn;
        }

        // 그리는 선이 없고, 지운 객체도 장면에서 모두 사라졌는가(Reset 뒤 한 프레임).
        public bool IsClear => _strokes.IsClear && _explosions.IsClear;

        // 판이 바뀌거나 판을 정리할 때 모든 선과 링을 지운다. 새 판의 기록 번호는 1부터다.
        public void Reset()
        {
            _strokes.Reset();
            _explosions.Reset();
            _drawn = 0;
        }

        public void Dispose()
        {
            _strokes.Dispose();
            _explosions.Dispose();
        }
    }
}
