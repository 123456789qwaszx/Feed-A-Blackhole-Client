using System;

namespace BlackHole.Core
{
    // 특수 성질 한 줄: 성질 ID와 사망 효과.
    [Serializable]
    public sealed class EnemyTraitData
    {
        public string Id;
        // 종류 이름이 비어 있으면 오류다 — 성질에는 효과가 있어야 한다.
        public DeathEffectData Effect;
        // 이 성질의 동시 상한(살아 있는 그 성질 적 + Breaker에 남은 그 버프 중첩). 0이면 상한이 없다.
        public int MaxActive;
    }
}
