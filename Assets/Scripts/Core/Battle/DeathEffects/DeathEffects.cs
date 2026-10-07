using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 한 판의 사망 효과 대기열과 처리.
    // 대기열: 특수 적(성질이 붙은 적)이 피해로 죽는 순간(World.DealDamage) 그 성질의 효과·죽은 자리·마지막 피해의 출처를 넣음.
    // 처리: 넣은 순서(사망 순서)대로 효과를 실행하고 대기열을 비움.
    // 레이저 별은 죽는 순간 경로를 정해 예고를 시작하고, 예고 시간이 지난 Step에 쏜다(피해는 그때 준다).
    // 효과의 피해는 특수 적(성질이 붙은 적, 픽업 포함)에게 가지 않는다 — 효과가 효과를 부르지 않는다.
    public sealed class DeathEffects
    {
        private const float LaserReach = 45f;
        // 레이저 별의 예고 시간(초).
        private const float LaserTelegraphSeconds = 0.4f;
        private const float TimeEpsilon = 1e-5f;

        private readonly List<Pending> _pending = new();
        private readonly List<LightningHit> _lightningHits = new();
        private readonly List<ExplosionBlast> _explosions = new();
        private readonly List<LaserBurst> _laserBursts = new();
        private readonly List<ChargingLaser> _chargingLasers = new();
        private readonly List<LaserTelegraph> _laserTelegraphs = new();
        private readonly List<Enemy> _targets = new();
        private readonly HashSet<Enemy> _struck = new();
        // 번개의 갈래·치명타, 레이저의 방향·치명타 판정.
        private readonly BattleRandom _random;
        private long _nextSequence = 1;

        private readonly struct Pending
        {
            public DeathEffectDefinition Effect { get; }
            public Point2 Position { get; }
            public PlayerId Source { get; }
            // 죽은 적의 크기 배율(반지름 ÷ 크기 1의 반지름, SizeRule). 폭발 반지름이 이만큼 커진다.
            public float SizeScale { get; }
            // 죽은 적 성질의 동시 상한(EnemyTraitDefinition.MaxAlive). 달 중첩의 상한으로 쓴다. 0이면 상한이 없다.
            public int MaxStacks { get; }

            public Pending(DeathEffectDefinition effect, Point2 position, PlayerId source, float sizeScale, int maxStacks)
            {
                Effect = effect;
                Position = position;
                Source = source;
                SizeScale = sizeScale;
                MaxStacks = maxStacks;
            }
        }

        // 예고 중인 레이저. 경로·정의·출처는 예고를 시작할 때 정해진다.
        private sealed class ChargingLaser
        {
            public Point2 Start;
            public Point2 End;
            public LaserBurstDefinition Definition;
            public PlayerId Source;
            public float Remaining;
        }

        // 마지막 진행 동안의 번개 이동, 폭발, 레이저 발동(일어난 순서).
        public IReadOnlyList<LightningHit> LightningHits { get; }
        public IReadOnlyList<ExplosionBlast> Explosions { get; }
        public IReadOnlyList<LaserBurst> LaserBursts { get; }

        // 지금 예고 중인 레이저(예고한 순서). 화면은 이것으로 예고선을 그린다. 처리(Resolve)마다 지금 상태로 다시 채운다.
        public IReadOnlyList<LaserTelegraph> LaserTelegraphs { get; }

        // 처리되지 않은 효과(예고 중인 레이저 포함)가 남아 있는가.
        public bool HasPending => _pending.Count > 0 || _chargingLasers.Count > 0;

        internal DeathEffects(int seed)
        {
            _random = new BattleRandom(seed, BattleRandom.DeathEffectStream);
            LightningHits = _lightningHits.AsReadOnly();
            Explosions = _explosions.AsReadOnly();
            LaserBursts = _laserBursts.AsReadOnly();
            LaserTelegraphs = _laserTelegraphs.AsReadOnly();
        }

        // 막 죽은 적(피해로 처음 죽음)이 특수 적이면 그 성질의 효과를 대기열에 추가.
        // [보류] 파괴 요청으로 죽은 적은 여기로 오지 않는다(World.ProcessDestroyRequests 참고).
        internal void Enqueue(Enemy enemy, PlayerId source)
        {
            if (enemy.Trait != null)
                _pending.Add(new Pending(enemy.Trait.Effect, enemy.Position, source, enemy.Stats.Radius / enemy.Definition.Radius, enemy.Trait.MaxAlive));
        }

        internal void BeginAdvance()
        {
            _lightningHits.Clear();
            _explosions.Clear();
            _laserBursts.Clear();
        }

        // 판 정리: 처리되지 않은 효과와 예고 중인 레이저를 버린다(쏘지 않는다).
        internal void Clear()
        {
            _pending.Clear();
            _chargingLasers.Clear();
            _laserTelegraphs.Clear();
        }

        // 먼저 예고가 끝난 레이저를 쏘고, 그다음 이번 Step의 사망 효과를 처리한다.
        // 이번 Step에 예고를 시작한 레이저는 다음 Step부터 시간이 흐른다.
        internal void Resolve(World world, float delta)
        {
            FireChargedLasers(delta, world);

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
                    case LaserBurstDefinition laser:
                        Charge(laser, pending);
                        break;
                    case MoonBuffDefinition:
                        foreach (BattlePlayer player in world.Players)
                            player.Breaker?.GrantMoon(pending.MaxStacks);
                        break;
                    case CometBuffDefinition:
                        foreach (BattlePlayer player in world.Players)
                            player.Breaker?.GrantComet();
                        break;
                    case GoldenDefinition:
                        // 황금의 Gold 배율은 출현 때 그 적의 수치에 들어 있고, 사망 확정 순간 판의 합계에 이미 들었다.
                        break;
                }
            }

            _pending.Clear();
            RefreshTelegraphs();
        }

        // 효과 피해를 받을 수 있는 적: 살아 있고 특수 적이 아닌 적(World.Enemies는 살아 있는 적뿐이다).
        private static bool CanBeStruck(Enemy enemy) => !enemy.IsSpecial;

        // 줄기 하나를 내보내고, 갈래 확률로 성공할 때마다 하나를 더 내보낸다(최대 MaxBranches). 줄기마다 MaxTargets만큼 연쇄한다.
        // 맞힌 적 목록은 한 발동의 모든 줄기가 함께 쓴다 — 같은 적을 두 번 맞히지 않는다.
        // 치명타는 발동 한 번에 한 번 판정한다(원작: 전기가 일어날 때 함께 판정). 모든 줄기·적중이 같은 결과를 쓴다.
        private void Chain(ChainLightningDefinition chain, Pending pending, World world)
        {
            _struck.Clear();
            int branches = 1;

            while (branches < ChainLightningDefinition.MaxBranches && chain.BranchChance > 0 && _random.NextFloat() < chain.BranchChance)
                branches++;

            bool critical = chain.CritChance > 0 && _random.NextFloat() < chain.CritChance;

            for (int branch = 0; branch < branches; branch++)
                ChainBranch(chain, pending, world, critical);
        }

        private void ChainBranch(ChainLightningDefinition chain, Pending pending, World world, bool critical)
        {
            Point2 origin = pending.Position;

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

                    float gap = (float)Math.Sqrt(origin.DistanceSquared(enemy.Position)) - enemy.Stats.Radius;

                    if (nearest == null || gap < nearestGap)
                    {
                        nearest = enemy;
                        nearestGap = gap;
                    }
                }

                if (nearest == null)
                    break;

                float amount = critical ? chain.Damage * chain.CritMultiplier : chain.Damage;

                _struck.Add(nearest);
                _lightningHits.Add(new LightningHit(_nextSequence++, origin, nearest.Position, critical));
                origin = nearest.Position;
                world.DealDamage(nearest, new Damage(amount, pending.Source, critical));
            }
        }

        // 폭발 반지름 = 정의의 반지름(크기 1 별 기준) × 죽은 별의 크기 배율. 별이 클수록 폭발도 같은 배율로 넓다.
        private void Explode(ExplosionDefinition explosion, Pending pending, World world)
        {
            _targets.Clear();
            float radius = explosion.Radius * pending.SizeScale;
            IReadOnlyList<Enemy> enemies = world.Enemies;

            for (int i = 0; i < enemies.Count; i++)
            {
                if (CanBeStruck(enemies[i]) && enemies[i].IsWithin(pending.Position, radius))
                    _targets.Add(enemies[i]);
            }

            foreach (Enemy target in _targets)
                world.DealDamage(target, new Damage(explosion.DamageTo(target), pending.Source));

            _explosions.Add(new ExplosionBlast(_nextSequence++, pending.Position, radius, _targets.Count));
        }

        // 레이저 별이 죽은 순간: 경로를 정하고 예고를 시작한다. 피해는 예고가 끝날 때 준다(FireChargedLasers).
        private void Charge(LaserBurstDefinition laser, Pending pending)
        {
            Point2 center = pending.Position;
            double angle = _random.NextFloat() * 2 * Math.PI;
            float directionX = (float)Math.Cos(angle);
            float directionY = (float)Math.Sin(angle);

            // 죽은 자리를 지나는 직선을 양쪽으로 화면 밖까지 뻗는다.
            _chargingLasers.Add(new ChargingLaser
            {
                Start = new Point2(center.X - LaserReach * directionX, center.Y - LaserReach * directionY),
                End = new Point2(center.X + LaserReach * directionX, center.Y + LaserReach * directionY),
                Definition = laser,
                Source = pending.Source,
                Remaining = LaserTelegraphSeconds,
            });
        }

        // 예고 시간을 흘리고, 예고가 끝난 레이저를 예고한 순서대로 쏜다.
        private void FireChargedLasers(float delta, World world)
        {
            for (int i = 0; i < _chargingLasers.Count;)
            {
                ChargingLaser charging = _chargingLasers[i];
                charging.Remaining -= delta;

                if (charging.Remaining > TimeEpsilon)
                {
                    i++;
                    continue;
                }

                _chargingLasers.RemoveAt(i);
                Fire(charging, world);
            }
        }

        // 예고가 끝난 레이저: 경로의 너비 안에 닿는 적 전부에게 한 번 피해를 준다. 치명타는 발사 한 번에 한 번 판정한다.
        private void Fire(ChargingLaser charging, World world)
        {
            LaserBurstDefinition laser = charging.Definition;
            Point2 start = charging.Start;
            Point2 end = charging.End;

            _targets.Clear();
            float halfWidth = laser.Width / 2;
            IReadOnlyList<Enemy> enemies = world.Enemies;

            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];

                if (!CanBeStruck(enemy))
                    continue;

                // 레이저 선분에서 이 적과 가장 가까운 지점.
                Point2 nearestPoint = NearestOnSegment(enemy.Position, start, end);

                if (enemy.IsWithin(nearestPoint, halfWidth))
                    _targets.Add(enemy);
            }

            bool critical = laser.CritChance > 0
                            && _random.NextFloat() < laser.CritChance;

            var damage = new Damage(critical
                ? laser.Damage * laser.CritMultiplier
                : laser.Damage, charging.Source, critical);

            foreach (Enemy target in _targets)
                world.DealDamage(target, damage);

            _laserBursts.Add(new LaserBurst(_nextSequence++, start, end, laser, critical, _targets.Count));
        }

        private void RefreshTelegraphs()
        {
            _laserTelegraphs.Clear();

            for (int i = 0; i < _chargingLasers.Count; i++)
            {
                ChargingLaser charging = _chargingLasers[i];
                _laserTelegraphs.Add(new LaserTelegraph(charging.Start, charging.End, charging.Remaining, LaserTelegraphSeconds));
            }
        }

        private static Point2 NearestOnSegment(Point2 point, Point2 start, Point2 end)
        {
            float pathX = end.X - start.X;
            float pathY = end.Y - start.Y;

            float pathLengthSquared = pathX * pathX + pathY * pathY;
            if (pathLengthSquared == 0f)
                return start;

            float toPointX = point.X - start.X;
            float toPointY = point.Y - start.Y;

            // point에서 경로 직선에 수선을 내린 위치를 구한다(0 = 시작, 1 = 끝).
            // 그 위치가 선분 밖이면 가까운 끝점으로 제한함.
            float dot = toPointX * pathX + toPointY * pathY;
            float pathRatio = Math.Clamp(dot / pathLengthSquared, 0f, 1f);

            return new Point2(
                start.X + pathRatio * pathX,
                start.Y + pathRatio * pathY);
        }
    }
}
