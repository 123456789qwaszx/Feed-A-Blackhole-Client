using System;

namespace BlackHole.Core
{
    [Serializable]
    public sealed class BreakerData
    {
        public float Damage;
        public float Interval;             // 공격 주기(초).
        public float Radius;               // 조준점을 중심으로 한 공격 원의 반지름.
        public float CritChance;           // 한 Tick이 치명타일 확률(0 ~ 1).
        public float CritDamage;           // 치명타 피해 보너스(0 이상). 1이면 +100%(2배)
        public float MoonDuration;         // 달 버프 중첩 하나의 지속 시간(초)
        public float MoonSpeedBonus;       //
        public float MoonRadiusBonus;      //
        public float CometDuration;        // 혜성 버프 중첩 하나의 지속 시간(초)
        public float CometCritDamageBonus; //
        public float PlanetBonusDamage;    // 행성에 주는 추가 피해
        public float StarBonusDamage;      // 별에 주는 추가 피해
    }
}
