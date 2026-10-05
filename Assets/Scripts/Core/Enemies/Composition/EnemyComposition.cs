using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 전투 시작 시, 업그레이드 표로만 결정되는 적 한 종류의 구성 값:
    // 질량과 그로 정해지는 색 비율,
    // 크기,
    // 더할 시작 공급 수,
    // Level업마다의 성장 공급 수,
    // 다음 종류로의 변환 비율,
    // 특수 성질마다의 생성 확률과 노드가 반영된 성질,
    // 픽업이면 등장 확률.
    public readonly struct EnemyComposition
    {
        private static readonly IReadOnlyList<EnemyTraitDefinition> NoTraits = Array.AsReadOnly(Array.Empty<EnemyTraitDefinition>());
        private static readonly IReadOnlyList<float> NoChances = Array.AsReadOnly(Array.Empty<float>());

        // 질량(%, 기본 100). 색 비율(TierRatios)을 정한다(MassRule).
        public float Mass { get; }

        // 색 등급마다 나오는 비율(종류의 색 등급 표 순서, 합 1). 질량으로 정해진다.
        public IReadOnlyList<float> TierRatios { get; }

        // 크기(기본 1). 크기 1부터 이 값까지가 같은 몫으로 섞여 나온다(SizeRule).
        public int Size { get; }

        // 콘텐츠의 전투 시작 공급에 더해 이 종류를 몇 마리 더 공급하는가.
        public int StartSupplyBonus { get; }

        // 블랙홀이 Level업할 때마다 이 종류를 몇 마리 요청하는가.
        public int GrowthSupply { get; }

        // 이 종류의 생성 중 변환 대상 종류(EnemyDefinition.UpgradesTo)로 나오는 몫(0 ~ 1). 노드를 사야 0보다 크다.
        public float UpgradeRatio { get; }

        // 이 판의 성질(종류의 성질 순서, 노드가 반영된 수치 — 예: 황금 배율). 출현한 적은 이 객체를 받는다.
        public IReadOnlyList<EnemyTraitDefinition> Traits { get; }

        // Traits[i]가 붙는 확률(0 ~ 1). 합은 1 이하다. 기본 0 — 확률 노드를 사야 붙는다. 픽업은 쓰지 않는다(성질이 언제나 붙는다).
        public IReadOnlyList<float> TraitChances { get; }

        // 픽업이면: 등장 주기마다 하나가 나올 확률(0 ~ 1). 기본 0 — 확률 노드를 사야 나온다. 픽업이 아니면 0이다.
        public float AppearChance { get; }

        public EnemyComposition(
            float mass,
            IReadOnlyList<float> tierRatios,
            int size = SizeRule.Base,
            int startSupplyBonus = 0,
            int growthSupply = 0,
            float upgradeRatio = 0,
            IReadOnlyList<EnemyTraitDefinition> traits = null,
            IReadOnlyList<float> traitChances = null,
            float appearChance = 0)
        {
            if (tierRatios == null || tierRatios.Count == 0)
                throw new ArgumentException("색 비율이 하나 이상 필요하다.", nameof(tierRatios));

            if ((traits?.Count ?? 0) != (traitChances?.Count ?? 0))
                throw new ArgumentException("성질 수와 성질 확률 수가 다르다.", nameof(traitChances));

            Mass = mass;
            TierRatios = tierRatios;
            Size = size;
            StartSupplyBonus = startSupplyBonus;
            GrowthSupply = growthSupply;
            UpgradeRatio = upgradeRatio;
            Traits = traits ?? NoTraits;
            TraitChances = traitChances ?? NoChances;
            AppearChance = appearChance;
        }

        // 성질 확률의 합.
        public float TraitChanceSum
        {
            get
            {
                float sum = 0;

                for (int i = 0; i < TraitChances.Count; i++)
                    sum += TraitChances[i];

                return sum;
            }
        }

        // 이 판 구성의 성질 중 trait(같은 객체)의 번호. 없으면 -1.
        public int IndexOfTrait(EnemyTraitDefinition trait)
        {
            for (int i = 0; i < Traits.Count; i++)
            {
                if (ReferenceEquals(Traits[i], trait))
                    return i;
            }

            return -1;
        }

        // 노드를 하나도 사지 않은 판 구성: 질량 100%(첫 색만), 크기 1, 성질은 종류의 것 그대로, 확률은 모두 0.
        public static EnemyComposition Base(EnemyDefinition kind) =>
            new(MassRule.Base, RatiosOf(kind, MassRule.Base),
                traits: kind.Traits, traitChances: new float[kind.Traits.Count]);

        // 업그레이드 표로 이 종류의 판 구성을 계산한다.
        public static EnemyComposition From(EnemyDefinition kind, UpgradeTable upgrades)
        {
            float mass = upgrades.Apply(EnemyUpgradeStats.Mass(kind.Id), MassRule.Base);
            int size = Whole(upgrades.Apply(EnemyUpgradeStats.Size(kind.Id), SizeRule.Base));
            int startSupply = Whole(upgrades.Apply(EnemyUpgradeStats.StartSupply(kind.Id), 0));
            int growthSupply = Whole(upgrades.Apply(EnemyUpgradeStats.GrowthSupply(kind.Id), 0));
            float upgrade = Percent(upgrades.Apply(EnemyUpgradeStats.Upgrade(kind.Id), 0), kind.Id, "변환 비율");
            float appear = Percent(upgrades.Apply(EnemyUpgradeStats.Chance(kind.Id), 0), kind.Id, "등장 확률");

            if (float.IsNaN(mass) || float.IsInfinity(mass) || mass < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(upgrades), $"'{kind.Id}'의 질량은 0 이상의 유한한 값이어야 한다. 업그레이드 합: {mass}%.");

            if (size < SizeRule.Base || size > SizeRule.Max)
                throw new ArgumentOutOfRangeException(
                    nameof(upgrades), $"'{kind.Id}'의 크기는 {SizeRule.Base}부터 {SizeRule.Max}까지다. 업그레이드 합: {size}.");

            // 변환 대상이 없는 종류는 변환해 나올 종류가 없다. 판 조립과 로드 때의 검사(UpgradeContentCheck)가 이 예외를 본다.
            if (upgrade > 0 && kind.UpgradesTo == null)
                throw new ArgumentException($"'{kind.Id}'에는 변환 대상이 없어 변환 비율을 둘 수 없다. 업그레이드 합: {upgrade * 100:0.##}%.", nameof(upgrades));

            // 픽업은 공급되지 않고 주기마다 등장 확률로 나온다. 공급·질량·크기 노드와 등장 확률은 서로의 종류에만 뜻이 있다.
            if (kind.IsPickup && (startSupply > 0 || growthSupply > 0))
                throw new ArgumentException($"'{kind.Id}'는 픽업이라 공급되지 않는다. 공급 수 노드를 둘 수 없다.", nameof(upgrades));

            if (kind.IsPickup && (mass != MassRule.Base || size != SizeRule.Base))
                throw new ArgumentException($"'{kind.Id}'는 픽업이라 질량·크기가 없다. 질량·크기 노드를 둘 수 없다.", nameof(upgrades));

            if (!kind.IsPickup && appear > 0)
                throw new ArgumentException($"'{kind.Id}'는 픽업이 아니라 등장 확률을 둘 수 없다. 특수 성질은 성질 확률(trait.<성질>.chance)을 쓴다.", nameof(upgrades));

            var traits = new EnemyTraitDefinition[kind.Traits.Count];
            var chances = new float[kind.Traits.Count];
            float chanceSum = 0;

            for (int i = 0; i < traits.Length; i++)
            {
                EnemyTraitDefinition trait = kind.Traits[i];
                traits[i] = Upgraded(kind, trait, upgrades);

                // 픽업의 성질은 언제나 붙으므로 확률이 없다.
                if (kind.IsPickup)
                    continue;

                chances[i] = Percent(upgrades.Apply(EnemyUpgradeStats.TraitChance(kind.Id, trait.Id), 0), kind.Id, $"'{trait.Id}' 성질 확률");
                chanceSum += chances[i];
            }

            // 성질은 한 마리에 하나다(배타). 합이 100%를 넘으면 어느 성질도 약속한 몫을 받을 수 없다.
            if (chanceSum > 1 + 1e-4f)
                throw new ArgumentException($"'{kind.Id}'의 성질 확률 합이 100%를 넘는다({chanceSum * 100:0.##}%).", nameof(upgrades));

            return new EnemyComposition(mass, RatiosOf(kind, mass), size, startSupply, growthSupply, upgrade,
                Array.AsReadOnly(traits), Array.AsReadOnly(chances), appear);
        }

        private static IReadOnlyList<float> RatiosOf(EnemyDefinition kind, float mass) =>
            Array.AsReadOnly(MassRule.TierRatios(mass, kind.Tiers.Count));

        // 노드가 성질의 수치를 바꾸는 것: 지금은 황금 배율뿐이다. 바뀌지 않으면 종류의 성질 객체를 그대로 쓴다.
        private static EnemyTraitDefinition Upgraded(EnemyDefinition kind, EnemyTraitDefinition trait, UpgradeTable upgrades)
        {
            if (trait.Effect is GoldenDefinition golden)
            {
                float multiplier = upgrades.Apply(EnemyUpgradeStats.TraitMultiplier(kind.Id, trait.Id), golden.Multiplier);

                if (multiplier != golden.Multiplier)
                    return trait.With(new GoldenDefinition(multiplier));
            }

            return trait;
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
