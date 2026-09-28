namespace BlackHole.Core
{
    // 적 하나의 실행 수치. 판 조립 때 (종류, 색 등급)마다 한 번 계산되고(EnemyStatTable),
    // 출현한 적은 그 값을 받아 살아 있는 동안 바뀌지 않는다.
    public readonly struct EnemyStats
    {
        public float MaxHealth { get; }
        // 이동 속도(초당 거리). 행동이 이 값을 읽는다.
        public float MoveSpeed { get; }
        // 크기(반지름). 화면이 이 값으로 그린다.
        public float Size { get; }
        // 이 적의 사망이 확정되는 순간 판의 Gold 합계에 드는 값. 같은 판의 같은 색은 모두 같은 값이다.
        public long Gold { get; }
        // 이 적의 사망이 확정되는 순간 블랙홀에 드는 EXP. 색 등급의 값 그대로다 — 질량 단계와 황금은 곱하지 않는다(BLACKHOLE_GROWTH_PLAN 4.1).
        public long Exp { get; }

        public EnemyStats(float maxHealth, float moveSpeed, float size, long gold, long exp = 0)
        {
            MaxHealth = DefinitionGuard.Positive(maxHealth, nameof(maxHealth));
            MoveSpeed = DefinitionGuard.Positive(moveSpeed, nameof(moveSpeed));
            Size = DefinitionGuard.Positive(size, nameof(size));
            Gold = DefinitionGuard.NotNegative(gold, nameof(gold));
            Exp = DefinitionGuard.NotNegative(exp, nameof(exp));
        }
    }
}
