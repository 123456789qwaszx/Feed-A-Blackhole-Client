using System;

namespace BlackHole.Core
{
    // 판 조립이 받는 적 종류 하나의 판 구성 값(BATTLE_COMPOSITION_PLAN 4.2, BLACKHOLE_LEVEL_PLAN 4.3):
    // 질량 단계, 황금 비율, 황금 배율, 더할 시작 공급 수, Level업마다의 성장 공급 수, 다음 종류로의 변환 비율, 특수 종류의 생성 확률.
    // 판 조립 때 업그레이드 표에서 계산된다(From). 보정이 없으면 Base(종류)다. 색 비율은 여기에 없다 — 블랙홀 Level이 정한다.
    public readonly struct EnemyComposition
    {
        // 질량 단계(종류의 질량 단계 표 번호). 질량 증가를 산 수다.
        public int MassLevel { get; }
        // 황금으로 나오는 비율(0 ~ 1).
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
            if (massLevel < 0)
                throw new ArgumentOutOfRangeException(nameof(massLevel), "0 이상이 필요하다.");

            if (startSupplyBonus < 0)
                throw new ArgumentOutOfRangeException(nameof(startSupplyBonus), "0 이상이 필요하다.");

            if (growthSupply < 0)
                throw new ArgumentOutOfRangeException(nameof(growthSupply), "0 이상이 필요하다.");

            if (float.IsNaN(goldenRatio) || goldenRatio < 0 || goldenRatio > 1)
                throw new ArgumentOutOfRangeException(nameof(goldenRatio), "0부터 1까지다.");

            if (float.IsNaN(goldenMultiplier) || float.IsInfinity(goldenMultiplier) || goldenMultiplier < 0)
                throw new ArgumentOutOfRangeException(nameof(goldenMultiplier), "0 이상의 유한한 값이 필요하다.");

            if (float.IsNaN(upgradeRatio) || upgradeRatio < 0 || upgradeRatio > 1)
                throw new ArgumentOutOfRangeException(nameof(upgradeRatio), "0부터 1까지다.");

            if (float.IsNaN(specialChance) || specialChance < 0 || specialChance > 1)
                throw new ArgumentOutOfRangeException(nameof(specialChance), "0부터 1까지다.");

            MassLevel = massLevel;
            GoldenRatio = goldenRatio;
            GoldenMultiplier = goldenMultiplier;
            StartSupplyBonus = startSupplyBonus;
            GrowthSupply = growthSupply;
            UpgradeRatio = upgradeRatio;
            SpecialChance = specialChance;
        }

        // 보정이 없을 때: 질량 단계 0, 황금 비율 0, 황금 배율은 종류의 기본값, 더할 시작 공급·성장 공급·변환·특수 확률 없음.
        public static EnemyComposition Base(EnemyDefinition kind) =>
            new EnemyComposition(0, 0, kind.GoldenMultiplier);

        // 업그레이드 표로 이 종류의 판 구성을 계산한다. 수치 이름은 EnemyUpgradeStats다. stage는 이 판의 성장도다.
        // 각 수치의 기본값(질량 단계 0, 황금 비율 0, 황금 배율 = 종류의 기본값, 더할 공급 0, 성장 공급 0,
        // 변환 = 그 성장도의 기본 변환 비율(EnemyDefinition.BaseUpgradeAt, 없으면 0%), 특수 확률 0%)에
        // 표의 합성 규칙을 적용한 뒤 적 시스템의 한계를 건다:
        // - 질량 단계와 더할 공급 수·성장 공급 수는 가장 가까운 정수로 읽는다 [임시]. 질량 단계는 그 종류의 질량 단계 표 안이어야 한다.
        // - 황금 비율은 1을 넘지 않는다. 황금이 되지 않는 종류의 황금 비율은 0보다 클 수 없다.
        // - 변환과 특수 확률은 %다. 100에서 멈춘다(100을 넘는 원작 값의 뜻은 [미정]). 변환 대상이 없는 종류의 변환, 특수 종류가 아닌 종류의 확률은 올릴 수 없다.
        // 한계 밖은 예외다 — 노드 저작 오류이며 UpgradeContentCheck가 로드 때 찾는다.
        public static EnemyComposition From(EnemyDefinition kind, UpgradeTable upgrades, int stage = HqGrowthDefinition.StartStage)
        {
            if (kind == null)
                throw new ArgumentNullException(nameof(kind));

            if (upgrades == null)
                throw new ArgumentNullException(nameof(upgrades));

            int massLevel = Whole(upgrades.Apply(EnemyUpgradeStats.MassLevel(kind.Id), 0));
            float goldenRatio = Math.Min(1, upgrades.Apply(EnemyUpgradeStats.GoldenRatio(kind.Id), 0));
            float goldenMultiplier = upgrades.Apply(EnemyUpgradeStats.GoldenMultiplier(kind.Id), kind.GoldenMultiplier);
            int startSupply = Whole(upgrades.Apply(EnemyUpgradeStats.StartSupply(kind.Id), 0));
            int growthSupply = Whole(upgrades.Apply(EnemyUpgradeStats.GrowthSupply(kind.Id), 0));
            float upgrade = Percent(upgrades.Apply(EnemyUpgradeStats.Upgrade(kind.Id), kind.BaseUpgradeAt(stage)), kind.Id, "변환 비율");
            float chance = Percent(upgrades.Apply(EnemyUpgradeStats.Chance(kind.Id), 0), kind.Id, "생성 확률");

            if (massLevel >= kind.MassLevels.Count)
                throw new ArgumentOutOfRangeException(
                    nameof(upgrades), $"'{kind.Id}'의 질량 단계는 0부터 {kind.MassLevels.Count - 1}까지다. 업그레이드 합: {massLevel}.");

            if (goldenRatio > 0 && !kind.CanBeGolden)
                throw new ArgumentException($"'{kind.Id}'는 황금이 되지 않아 황금 비율을 올릴 수 없다.", nameof(upgrades));

            if (upgrade > 0 && kind.UpgradesTo == null)
                throw new ArgumentException($"'{kind.Id}'는 변환 대상이 없어 변환 비율을 올릴 수 없다.", nameof(upgrades));

            if (chance > 0 && !kind.IsSpecial)
                throw new ArgumentException($"'{kind.Id}'는 특수 종류가 아니어서 생성 확률을 올릴 수 없다.", nameof(upgrades));

            return new EnemyComposition(massLevel, goldenRatio, goldenMultiplier, startSupply, growthSupply, upgrade, chance);
        }

        private static int Whole(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

        // % 값을 몫(0 ~ 1)으로 읽는다. 100에서 멈춘다. 음수·무한은 예외다.
        private static float Percent(float value, string kindId, string label)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), $"'{kindId}'의 {label}은 0 이상의 유한한 값이어야 한다. 업그레이드 합: {value}.");

            return Math.Min(1, value / 100);
        }
    }
}
