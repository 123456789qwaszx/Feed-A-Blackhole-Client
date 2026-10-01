using System;

namespace BlackHole.Core
{
    // 사망 효과 종류마다 쓰는 칸이 다르다. 종류가 쓰지 않는 칸은 읽지 않는다.
    [Serializable]
    public sealed class DeathEffectData
    {
        // 종류 이름. 가능한 값은 EnemyContentLoader의 해석 목록에 있다.
        public string Kind;
        // Golden(Gold 배율)
        public float Multiplier;
        // ChainLightning, LaserBurst
        public float Damage;
        // ChainLightning(한 번 옮겨 가는 거리), Explosion(반경)
        public float Radius;
        // ChainLightning(한 줄기가 옮겨 가는 최대 횟수)
        public int MaxTargets;
        // ChainLightning(줄기가 하나 더 나갈 확률, 0 ~ 1)
        public float BranchChance;
        // ChainLightning, LaserBurst(치명타 확률 0 ~ 1, 치명타일 때 피해 배율)
        public float CritChance;
        public float CritMultiplier;
        // Explosion(대상 최대 HP에 대한 피해 비율, 0 ~ 1)
        public float HealthFraction;
        // LaserBurst(레이저 너비)
        public float Width;
        // MoonBuff·CometBuff는 쓰는 칸이 없다: 버프 시간과 수치는 Breaker의 것이다(BreakerData).
    }
}
