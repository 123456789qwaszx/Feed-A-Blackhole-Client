using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 사망 효과의 화면. 매 프레임 판의 효과 기록을 읽어 번개·폭발·레이저를 그린다.
    // 게임 상태를 바꾸지 않고, 피해를 다시 계산하지 않는다
    internal sealed class DeathEffectView : IDisposable
    {
        private readonly LightningBolts _lightning;
        private readonly ExplosionRings _explosions;
        private readonly LaserBeams _lasers;

        private long _drawn; // 그린 기록의 가장 큰 번호. 번개·폭발·레이저는 같은 번호 줄을 쓴다.

        public DeathEffectView(Transform parent, LightningLook lightningLook, ExplosionLook explosionLook)
        {
            _lightning = new LightningBolts(parent, lightningLook);
            _explosions = new ExplosionRings(parent, explosionLook);
            _lasers = new LaserBeams(parent);
        }

        public void Synchronize(World world, bool paused, float delta)
        {
            if (!paused)
            {
                _lightning.Age(delta);
                _explosions.Age(delta);
                _lasers.Age(delta);
            }

            long drawn = _drawn;
            IReadOnlyList<LightningHit> hits = world.DeathEffects.LightningHits;
            // 연쇄: 판은 옮겨 간 순서대로 기록한다. 앞 줄기가 끝난 자리에서 시작하는 줄기는 같은 연쇄의 다음 줄기다.
            int hop = 0;
            Point2? chainEnd = null;

            for (int i = 0; i < hits.Count; i++)
            {
                if (hits[i].Sequence <= _drawn)
                    continue;

                drawn = Math.Max(drawn, hits[i].Sequence);
                hop = chainEnd.HasValue && chainEnd.Value.Equals(hits[i].From) ? hop + 1 : 0;
                chainEnd = hits[i].To;
                _lightning.Play(hits[i].From, hits[i].To, hop);
            }

            IReadOnlyList<ExplosionBlast> explosions = world.DeathEffects.Explosions;

            for (int i = 0; i < explosions.Count; i++)
            {
                if (explosions[i].Sequence <= _drawn)
                    continue;

                drawn = Math.Max(drawn, explosions[i].Sequence);
                _explosions.Play(explosions[i].Center, explosions[i].Radius);
            }

            IReadOnlyList<LaserBurst> lasers = world.DeathEffects.LaserBursts;

            for (int i = 0; i < lasers.Count; i++)
            {
                if (lasers[i].Sequence <= _drawn)
                    continue;

                drawn = Math.Max(drawn, lasers[i].Sequence);
                _lasers.Play(lasers[i].Start, lasers[i].End, lasers[i].Definition.Width);
            }

            _drawn = drawn;
            _lasers.ShowTelegraphs(world.DeathEffects.LaserTelegraphs);
        }

        public bool IsClear => _lightning.IsClear && _explosions.IsClear && _lasers.IsClear;

        public void Reset()
        {
            _lightning.Reset();
            _explosions.Reset();
            _lasers.Reset();
            _drawn = 0;
        }

        public void Dispose()
        {
            _lightning.Dispose();
            _explosions.Dispose();
            _lasers.Dispose();
        }
    }
}
