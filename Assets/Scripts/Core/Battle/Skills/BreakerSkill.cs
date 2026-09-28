using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 공격 Tick(World.Step의 2. Passive Attack 자리):
    // 빈 Tick도 주기를 소비.
    public sealed class BreakerSkill
    {
        // 진행 시간을 더한 값의 끝자리 오차. 이만큼 모자라도 Tick 시각에 닿은 것으로 본다.
        private const float TimeEpsilon = 1e-5f;

        private readonly BattleRandom _critical;
        private readonly List<Enemy> _targets = new();
        private readonly List<BreakerTick> _ticks = new();

        // 다음 Tick까지 남은 주기(기본 주기 기준).
        private float _untilNextTick;

        public BreakerDefinition Definition { get; }

        // 켜져 있는가.
        // 지금 끄고 켜는 곳은 개발용 스킬 콘솔뿐이다(게임 규칙으로 끄는 일은 없다).
        public bool Enabled { get; private set; } = true;

        // 지금까지 일어난 Tick 수. 빈 Tick도 센다.
        public int TickCount { get; private set; }

        // 마지막 Tick이 피해를 준 적의 수. 빈 Tick이면 0이다.
        public int LastTickHitCount { get; private set; }

        // 마지막 진행 동안의 Tick(일어난 순서). 다음 진행이 시작될 때 비운다.
        public IReadOnlyList<BreakerTick> Ticks { get; }

        // 공격 주기 감소 버프의 남은 시간(초)과 주기 배율. 버프가 없으면 0과 1이다.
        public float HasteRemaining { get; private set; }

        public float HasteMultiplier { get; private set; } = 1;

        // 확정 치명타 버프의 남은 시간(초). 버프가 없으면 0이다.
        public float GuaranteedCriticalRemaining { get; private set; }

        // critical은 이 Breaker의 치명타만 쓰는 난수다.
        internal BreakerSkill(BreakerDefinition definition, BattleRandom critical)
        {
            Definition = definition;
            _critical = critical;
            Ticks = _ticks.AsReadOnly();
        }

        public void SetEnabled(bool enabled)
        {
            if (Enabled == enabled)
                return;

            Enabled = enabled;
            _untilNextTick = 0;
        }

        internal void BeginAdvance() => _ticks.Clear();

        // 한 Step 동안 주기가 여러 번 차면 그만큼 Tick한다. 같은 Step 안의 Tick은 같은 조준점과 같은 적 위치를 본다.
        internal void Advance(float delta, BattlePlayer owner, World world)
        {
            if (Enabled)
            {
                _untilNextTick -= delta / HasteMultiplier;

                while (_untilNextTick <= TimeEpsilon)
                {
                    Tick(owner, world);
                    _untilNextTick += Definition.Interval;
                }
            }

            AgeBuffs(delta);
        }

        internal void GrantHaste(AttackHasteDefinition haste)
        {
            HasteMultiplier = HasteRemaining > 0 ? Math.Min(HasteMultiplier, haste.IntervalMultiplier) : haste.IntervalMultiplier;
            HasteRemaining = Math.Max(HasteRemaining, haste.Duration);
        }

        internal void GrantGuaranteedCritical(GuaranteedCriticalDefinition critical) =>
            GuaranteedCriticalRemaining = Math.Max(GuaranteedCriticalRemaining, critical.Duration);

        private void AgeBuffs(float delta)
        {
            HasteRemaining = Math.Max(0, HasteRemaining - delta);
            GuaranteedCriticalRemaining = Math.Max(0, GuaranteedCriticalRemaining - delta);

            if (HasteRemaining == 0)
                HasteMultiplier = 1;
        }

        private void Tick(BattlePlayer owner, World world)
        {
            TickCount++;
            _targets.Clear();
            Point2? center = owner.AimPoint;

            if (center.HasValue)
            {
                float radiusSquared = Definition.Radius * Definition.Radius;
                IReadOnlyList<Enemy> enemies = world.Enemies;

                for (int i = 0; i < enemies.Count; i++)
                {
                    if (enemies[i].Position.DistanceSquared(center.Value) <= radiusSquared)
                        _targets.Add(enemies[i]);
                }
            }

            bool critical = _targets.Count > 0 && RollCritical();
            var damage = new Damage(critical ? Definition.Damage * Definition.CritMultiplier : Definition.Damage, owner.Id);

            foreach (Enemy target in _targets)
                world.DealDamage(target, damage);

            LastTickHitCount = _targets.Count;
            _ticks.Add(new BreakerTick(TickCount, center, Definition.Radius, _targets.Count, critical));
        }

        // 확정 치명타 중이면 굴리지 않고 치명타다. 확률이 0이나 1이면 굴리지 않는다.
        private bool RollCritical()
        {
            if (GuaranteedCriticalRemaining > 0 || Definition.CritChance >= 1)
                return true;

            return Definition.CritChance > 0 && _critical.NextFloat() < Definition.CritChance;
        }
    }
}
