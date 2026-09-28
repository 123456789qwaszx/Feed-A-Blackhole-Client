using System;

namespace BlackHole.Core
{
    public sealed class Enemy
    {
        private readonly IEnemyBehavior _behavior;

        public EnemyId Id { get; }
        public EnemyDefinition Definition { get; }
        // 색 등급(종류의 색 등급 표 번호). 출현 때 정해지고 바뀌지 않는다. 화면은 이 번호의 색으로 그린다.
        public int Tier { get; }
        // 황금인가. 출현 때 정해지고 바뀌지 않는다 — 사망 때 굴리지 않는다. 황금이면 Stats.Gold에 황금 배율이 들어 있다.
        public bool IsGolden { get; }
        // 실행 수치. 출현 때 판의 적 수치 표에서 (종류, 색 등급, 황금)의 값을 받고 바뀌지 않는다.
        public EnemyStats Stats { get; }
        public float Health { get; private set; }
        public bool IsAlive { get; private set; } = true;
        public Point2 Position { get; private set; }
        // 마지막으로 피해를 준 Player. 기록일 뿐이며 보상 귀속 규칙으로 쓰지 않는다(귀속 정책은 미정).
        public PlayerId? LastDamageSource { get; private set; }

        internal Enemy(
            EnemyId id, EnemyDefinition definition, int tier, bool golden, EnemyStats stats, Point2 position, IEnemyBehavior behavior)
        {
            Id = id;
            Definition = definition;
            Tier = tier;
            IsGolden = golden;
            Stats = stats;
            Health = stats.MaxHealth;
            Position = position;
            _behavior = behavior ?? throw new ArgumentNullException(nameof(behavior));
        }

        internal void Move(float delta)
        {
            Position = _behavior.NextPosition(Position, Stats, delta);
        }

        // true는 이번 피해로 처음 죽었다는 뜻이다. 이미 죽은 적은 피해를 받지 않는다.
        internal bool ApplyDamage(Damage damage)
        {
            if (!IsAlive)
                return false;

            Health = Math.Max(0, Health - damage.Amount);
            LastDamageSource = damage.Source;

            if (Health > 0)
                return false;

            IsAlive = false;
            return true;
        }

        // 파괴 요청으로 죽는다. 피해·HP를 계산하지 않는다(HP는 그대로 남는다).
        // true는 이번에 처음 죽었다는 뜻이다.
        internal bool Destroy()
        {
            if (!IsAlive)
                return false;

            IsAlive = false;
            return true;
        }
    }
}
