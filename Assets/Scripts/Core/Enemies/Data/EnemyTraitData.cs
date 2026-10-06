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
        // 이 성질이 붙은 적의 동시 생존 상한. 0이면 상한이 없다.
        public int MaxAlive;
    }
}
