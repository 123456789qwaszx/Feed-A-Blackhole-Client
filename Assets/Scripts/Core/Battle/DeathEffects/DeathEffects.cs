using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 한 판의 사망 효과 대기열과 처리.
    // 대기열: 효과를 가진 적이 피해로 죽는 순간(World.DealDamage) 그 효과·죽은 자리·마지막 피해의 출처를 넣음.
    // 처리: 넣은 순서(사망 순서)대로 효과를 실행하고 대기열을 비움.
    public sealed class DeathEffects
    {
        private readonly List<Pending> _pending = new();
        private readonly List<LightningHit> _lightningHits = new();
        private readonly List<ExplosionBlast> _explosions = new();
        private readonly List<Enemy> _targets = new();
        private readonly HashSet<Enemy> _struck = new();
        private long _nextSequence = 1;

        private readonly struct Pending
        {
            public DeathEffectDefinition Effect { get; }
            public Point2 Position { get; }
            public PlayerId Source { get; }

            public Pending(DeathEffectDefinition effect, Point2 position, PlayerId source)
            {
                Effect = effect;
                Position = position;
                Source = source;
            }
        }

        // 마지막 진행 동안의 번개 이동과 폭발(일어난 순서).
        public IReadOnlyList<LightningHit> LightningHits { get; }
        public IReadOnlyList<ExplosionBlast> Explosions { get; }

        // 처리되지 않은 효과가 남아 있는가.
        public bool HasPending => _pending.Count > 0;

        internal DeathEffects()
        {
            LightningHits = _lightningHits.AsReadOnly();
            Explosions = _explosions.AsReadOnly();
        }

        // 막 죽은 적(피해로 처음 죽음)의 효과를 대기열에 추가.
        internal void Enqueue(Enemy enemy, PlayerId source)
        {
            if (enemy.Definition.DeathEffect != null)
                _pending.Add(new Pending(enemy.Definition.DeathEffect, enemy.Position, source));
        }

        internal void BeginAdvance()
        {
            _lightningHits.Clear();
            _explosions.Clear();
        }

        internal void Clear() => _pending.Clear();

        internal void Resolve(World world)
        {
            for (int i = 0; i < _pending.Count; i++)
            {
                Pending pending = _pending[i];

                switch (pending.Effect)
                {
                    case ChainLightningDefinition chain:
                        Chain(chain, pending, world);
                        break;
                    case ExplosionDefinition explosion:
                        Explode(explosion, pending, world);
                        break;
                    case AttackHasteDefinition haste:
                        foreach (BattlePlayer player in world.Players)
                            player.Breaker?.GrantHaste(haste);
                        break;
                    case GuaranteedCriticalDefinition critical:
                        foreach (BattlePlayer player in world.Players)
                            player.Breaker?.GrantGuaranteedCritical(critical);
                        break;
                }
            }

            _pending.Clear();
        }

        // 효과 피해를 받을 수 있는 적: 살아 있고 효과가 없는 적(World.Enemies는 살아 있는 적뿐이다).
        private static bool CanBeStruck(Enemy enemy) => enemy.Definition.DeathEffect == null;

        private void Chain(ChainLightningDefinition chain, Pending pending, World world)
        {
            _struck.Clear();
            Point2 origin = pending.Position;
            var damage = new Damage(chain.Damage, pending.Source);

            for (int hop = 0; hop < chain.MaxTargets; hop++)
            {
                // 가장 가까운 적은 적의 원 가장자리까지의 거리로 고른다.
                Enemy nearest = null;
                float nearestGap = 0;
                IReadOnlyList<Enemy> enemies = world.Enemies;

                for (int i = 0; i < enemies.Count; i++)
                {
                    Enemy enemy = enemies[i];

                    if (!CanBeStruck(enemy) || _struck.Contains(enemy) || !enemy.IsWithin(origin, chain.Radius))
                        continue;

                    float gap = (float)Math.Sqrt(origin.DistanceSquared(enemy.Position)) - enemy.Stats.Size;

                    if (nearest == null || gap < nearestGap)
                    {
                        nearest = enemy;
                        nearestGap = gap;
                    }
                }

                if (nearest == null)
                    break;

                _struck.Add(nearest);
                _lightningHits.Add(new LightningHit(_nextSequence++, origin, nearest.Position));
                origin = nearest.Position;
                world.DealDamage(nearest, damage);
            }
        }

        private void Explode(ExplosionDefinition explosion, Pending pending, World world)
        {
            _targets.Clear();
            IReadOnlyList<Enemy> enemies = world.Enemies;

            for (int i = 0; i < enemies.Count; i++)
            {
                if (CanBeStruck(enemies[i]) && enemies[i].IsWithin(pending.Position, explosion.Radius))
                    _targets.Add(enemies[i]);
            }

            var damage = new Damage(explosion.Damage, pending.Source);

            foreach (Enemy target in _targets)
                world.DealDamage(target, damage);

            _explosions.Add(new ExplosionBlast(_nextSequence++, pending.Position, explosion.Radius, _targets.Count));
        }
    }
}
