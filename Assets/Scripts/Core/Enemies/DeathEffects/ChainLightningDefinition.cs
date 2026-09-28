using System;

namespace BlackHole.Core
{
    // 연쇄 번개: 죽은 자리에서 가장 가까운 적으로 번개가 옮겨 가며 피해를 준다(원작의 전기 천체, REFERENCE_ANALYSIS R3).
    // 한 번 옮겨 가는 거리는 Radius 이하, 옮겨 가는 횟수는 MaxTargets 이하, 한 번 맞힌 적은 다시 맞히지 않는다.
    // 세부(가장 가까운 순, 거리가 같으면 목록 순서)는 [임시]다(SYSTEM_CATALOG S06).
    public sealed class ChainLightningDefinition : DeathEffectDefinition
    {
        // 무한 연쇄를 막는 상한은 MaxTargets 자체다. 이 값은 저작 실수(지나치게 큰 수)만 막는다.
        public const int MaxTargetsLimit = 64;

        public float Damage { get; }
        public float Radius { get; }
        public int MaxTargets { get; }

        public ChainLightningDefinition(float damage, float radius, int maxTargets)
        {
            Damage = DefinitionGuard.Positive(damage, nameof(damage));
            Radius = DefinitionGuard.Positive(radius, nameof(radius));

            if (maxTargets < 1 || maxTargets > MaxTargetsLimit)
                throw new ArgumentOutOfRangeException(nameof(maxTargets), $"1부터 {MaxTargetsLimit}까지의 정수가 필요하다.");

            MaxTargets = maxTargets;
        }
    }
}
