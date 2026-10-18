using System;

namespace BlackHole.Core
{
    // 전기 별의 번개: 전기 소행성의 연쇄 번개(ChainLightningDefinition)와 같은 규칙으로 시작하되 정의·기록(StarLightningHit)·피해 출처(DamageSource.StarLightning)·
    // 화면(StarLightningLook·StarLightningBolts)이 전부 따로다. 전기 별은 피해·연쇄 수가 소행성과 급이 달라 앞으로 규칙도 따로 바뀐다.
    // 지금의 규칙: 죽은 자리에서 가장 가까운 적으로 번개가 옮겨 가며 피해를 준다. 한 번 옮겨 가는 거리(다음 적의 원 가장자리까지)는 Radius 이하,
    // 한 줄기가 옮겨 가는 횟수는 MaxTargets 이하. 갈래: BranchChance로 성공할 때마다 줄기가 하나 더(최대 MaxBranches). 한 번 맞힌 적은 다시 맞히지 않는다.
    // 치명타: 발동 한 번에 한 번 CritChance로 판정하고, 치명타면 그 발동의 모든 적중 피해에 CritMultiplier를 곱한다(Breaker와 별개).
    // 노드(electricStar.*)는 EnemyUpgradeStats가 (별, 전기)로 매핑한다 — 소행성 노드와 섞이지 않는다.
    public sealed class StarLightningDefinition : DeathEffectDefinition
    {
        public override DeathEffectType Type => DeathEffectType.StarLightning;

        // 무한 연쇄를 막는 상한은 MaxTargets 자체다. 이 값은 저작 실수(지나치게 큰 수)만 막는다.
        public const int MaxTargetsLimit = 64;
        // 한 번의 발동에서 나가는 줄기의 상한.
        public const int MaxBranches = 8;

        public float Damage { get; }
        public float Radius { get; }
        public int MaxTargets { get; }
        public float BranchChance { get; }
        public float CritChance { get; }
        public float CritMultiplier { get; }

        public StarLightningDefinition(float damage, float radius, int maxTargets, float branchChance = 0, float critChance = 0, float critMultiplier = 1)
        {
            Damage = DefinitionGuard.Positive(damage, nameof(damage));
            Radius = DefinitionGuard.Positive(radius, nameof(radius));

            if (maxTargets < 1 || maxTargets > MaxTargetsLimit)
                throw new ArgumentOutOfRangeException(nameof(maxTargets), $"1부터 {MaxTargetsLimit}까지의 정수가 필요하다.");

            MaxTargets = maxTargets;
            BranchChance = Chance(branchChance, nameof(branchChance));
            CritChance = Chance(critChance, nameof(critChance));
            CritMultiplier = DefinitionGuard.Positive(critMultiplier, nameof(critMultiplier));
        }

        private static float Chance(float value, string name)
        {
            if (float.IsNaN(value) || value < 0 || value > 1)
                throw new ArgumentOutOfRangeException(name, "0부터 1까지의 값이 필요하다.");

            return value;
        }
    }
}
