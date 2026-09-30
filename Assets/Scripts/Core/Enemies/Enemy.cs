using System;

namespace BlackHole.Core
{
    public sealed class Enemy
    {
        public EnemyId Id { get; }
        public EnemyDefinition Definition { get; }

        // 색 등급(종류의 색 등급 표 번호).
        public int Tier { get; }

        // 황금인가. 출현 때 정해짐.
        public bool IsGolden { get; }

        // 적의 스탯 수치. 출현 때 판의 적 수치 표에서 (종류, 색 등급, 황금)의 값을 받고 받음..
        public EnemyStats Stats { get; }

        public float Health { get; private set; }

        public bool IsAlive { get; private set; } = true;

        public Point2 Position { get; private set; }

        public PlayerId? LastDamageSource { get; private set; }

        public event Action<Enemy> Damaged;

        internal Enemy(
            EnemyId id,
            EnemyDefinition definition,
            int tier,
            bool golden,
            EnemyStats stats,
            Point2 position)
        {
            Id = id;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Tier = tier;
            IsGolden = golden;
            Stats = stats;
            Health = stats.MaxHealth;
            Position = position;
        }

        // 점 point에서 거리 reach 안에 이 적의 원(반지름 = 크기)이 닿는가. 공격과 사망 효과가 적을 맞히는 판정이다.
        internal bool IsWithin(Point2 point, float reach)
        {
            float touch = reach + Stats.Size;
            return Position.DistanceSquared(point) <= touch * touch;
        }

        internal void Move(float delta)
        {
            Position = EnemyBehaviors.NextPosition(Position, Stats, delta);
        }

        internal bool ApplyDamage(Damage damage)
        {
            if (!IsAlive)
                return false;

            Health = Math.Max(0, Health - damage.Amount);
            LastDamageSource = damage.Source;
            Damaged?.Invoke(this);

            if (Health > 0)
                return false;

            IsAlive = false;
            return true;
        }

        internal bool Destroy()
        {
            if (!IsAlive)
                return false;

            IsAlive = false;
            return true;
        }
    }
}
