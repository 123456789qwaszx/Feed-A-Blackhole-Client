using System;

namespace BlackHole.Core
{
    // 사망 효과 종류마다 쓰는 칸이 다르다. 종류가 쓰지 않는 칸은 읽지 않는다.
    [Serializable]
    public sealed class DeathEffectData
    {
        // 종류 이름. 가능한 값은 EnemyContentLoader의 해석 목록에 있다. 비어 있으면 효과가 없다.
        public string Kind;
        // ChainLightning, Explosion
        public float Damage;
        // ChainLightning(한 번 옮겨 가는 거리), Explosion(반경)
        public float Radius;
        // ChainLightning(옮겨 가는 최대 횟수)
        public int MaxTargets;
        // AttackHaste, GuaranteedCritical(버프 시간, 초)
        public float Duration;
        // AttackHaste(공격 주기 배율, 0 ~ 1)
        public float IntervalMultiplier;
    }
}
