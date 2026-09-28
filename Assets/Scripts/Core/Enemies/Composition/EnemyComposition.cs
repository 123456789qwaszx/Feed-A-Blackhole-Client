using System;

namespace BlackHole.Core
{
    // 전투 시작 시, 결정되는 적 구성 값:
    // 질량 단계,
    // 황금 비율,
    // 황금 배율,
    // 더할 시작 공급 수,
    // Level업마다의 성장 공급 수,
    // 다음 종류로의 변환 비율,
    // 특수 종류의 생성 확률.
    public readonly struct EnemyComposition
    {
        // 적의 질량 단계. (질량 증가 노드)
        public int MassLevel { get; }

        // 소행성이 황금으로 나오는 비율. (노드 구매 이후 10% 고정)
        public float GoldenRatio { get; }

        // 황금일 때 Gold에 곱하는 값.
        public float GoldenMultiplier { get; }

        // 콘텐츠의 전투 시작 공급에 더해 이 종류를 몇 마리 더 공급하는가.
        public int StartSupplyBonus { get; }

        // 블랙홀이 Level업할 때마다 이 종류를 몇 마리 요청하는가(BLACKHOLE_GROWTH_PLAN 4.3).
        public int GrowthSupply { get; }

        // 이 종류의 생성 중 변환 대상 종류(EnemyDefinition.UpgradesTo)로 나오는 몫(0 ~ 1). 변환 대상이 없으면 0이다.
        public float UpgradeRatio { get; }

        // 특수 종류이면: 부모로 정해진 생성 중 이 종류로 나오는 몫(0 ~ 1). 기본 0 — 확률 노드를 사야 나온다.
        public float SpecialChance { get; }

        public EnemyComposition(
            int massLevel,
            float goldenRatio,
            float goldenMultiplier,
            int startSupplyBonus = 0,
            int growthSupply = 0,
            float upgradeRatio = 0,
            float specialChance = 0)
        {
            MassLevel = massLevel;
            GoldenRatio = goldenRatio;
            GoldenMultiplier = goldenMultiplier;
            StartSupplyBonus = startSupplyBonus;
            GrowthSupply = growthSupply;
            UpgradeRatio = upgradeRatio;
            SpecialChance = specialChance;
        }

        public static EnemyComposition Base(EnemyDefinition kind) =>
            new(0, 0, kind.GoldenMultiplier);

        // 업그레이드 표와 성장도를 기반으로
        // 적의 스탯 수치 계산.
        public static EnemyComposition From(
            EnemyDefinition kind,
            UpgradeTable upgrades,
            int stage = HqGrowthDefinition.StartStage)
        {
            int massLevel = Whole(upgrades.Apply(EnemyUpgradeStats.MassLevel(kind.Id), 0));
            float goldenRatio = Math.Min(1, upgrades.Apply(EnemyUpgradeStats.GoldenRatio(kind.Id), 0));
            float goldenMultiplier = upgrades.Apply(EnemyUpgradeStats.GoldenMultiplier(kind.Id), kind.GoldenMultiplier);
            int startSupply = Whole(upgrades.Apply(EnemyUpgradeStats.StartSupply(kind.Id), 0));
            int growthSupply = Whole(upgrades.Apply(EnemyUpgradeStats.GrowthSupply(kind.Id), 0));
            float upgrade = Percent(upgrades.Apply(EnemyUpgradeStats.Upgrade(kind.Id), kind.BaseUpgradeAt(stage)), kind.Id, "변환 비율");
            float chance = Percent(upgrades.Apply(EnemyUpgradeStats.Chance(kind.Id), 0), kind.Id, "생성 확률");

            return new EnemyComposition(massLevel, goldenRatio, goldenMultiplier, startSupply, growthSupply, upgrade, chance);
        }

        private static int Whole(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

        private static float Percent(float value, string kindId, string label)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value), $"'{kindId}'의 {label}은 0 이상의 유한한 값이어야 한다. 업그레이드 합: {value}.");
            }

            return Math.Min(1, value / 100);
        }
    }
}
