using System;

namespace BlackHole.Core
{
    [Serializable]
    public sealed class BreakerData
    {
        public float Damage;
        // 공격 주기(초).
        public float Interval;
        // 조준점을 중심으로 한 공격 원의 반지름.
        public float Radius;
        // 한 Tick이 치명타일 확률(0 ~ 1).
        public float CritChance;
        // 치명타 Tick의 피해 배율(1 이상).
        public float CritMultiplier;
    }
}
