using System;

namespace BlackHole.Core
{
    // 색 등급 한 줄.
    [Serializable]
    public sealed class EnemyTierData
    {
        public float MaxHealth;
        // 반지름.
        public float Size;
        // 사망 때 판의 합계에 드는 Gold의 기본값. 0 이상.
        public long Gold;
        // 사망 때 블랙홀에 드는 EXP. 0 이상. 질량 단계와 황금은 곱하지 않는다.
        public long Exp;
    }
}
