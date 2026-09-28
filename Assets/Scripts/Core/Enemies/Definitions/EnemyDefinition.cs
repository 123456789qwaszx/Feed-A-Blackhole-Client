using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 적 종류 하나의 공유 정의: 이동 속도, 색 등급 표, 성장도별 색 비율, 질량 단계 표, 황금 배율, 행동, 사망 효과, 변환 대상·부모 종류.
    // 종류는 계열(소행성·행성·별·달·혜성)이고 색은 종류 안에 둔다(BATTLE_COMPOSITION_PLAN 4.1). 색이 없는 종류는 색 등급이 한 줄이다.
    // 사망 효과는 종류에 붙는 특성이다(전기·폭발·처치 버프). HQ EXP는 색 등급마다 적는다(블랙홀 성장). 외형은 Core가 모른다(Unity 쪽 종류 에셋이 가진다).
    public sealed class EnemyDefinition
    {
        public string Id { get; }
        public float MoveSpeed { get; }
        // 색 등급 표. 번호가 적의 색 등급(Enemy.Tier)이다.
        public IReadOnlyList<EnemyTier> Tiers { get; }
        // 블랙홀 성장도별 색 비율. FromStage가 커지는 순서다. 하나 이상.
        public IReadOnlyList<StageColorDefinition> StageColors { get; }
        // 질량 단계 표(HP·Gold 계수). MassLevels[i]가 질량 단계 i다(0 = 질량 증가를 사지 않음).
        public IReadOnlyList<MassLevelDefinition> MassLevels { get; }
        // 황금일 때 그 적의 Gold에 곱하는 기본값. 0이면 이 종류는 황금이 되지 않는다(원작은 소행성만, 기본 50배).
        // 황금은 종류가 아니라 생성 때 정해지는 특성이다. 얼마나 섞일지(황금 비율)와 노드로 오른 배율은 판 구성(EnemyComposition)이 가진다.
        public float GoldenMultiplier { get; }
        public bool CanBeGolden => GoldenMultiplier > 0;
        public OrbitBehaviorDefinition Behavior { get; }
        // 이 종류가 죽을 때의 효과. 없으면 null이다. 효과를 가진 적은 사망 효과의 피해를 받지 않는다.
        public DeathEffectDefinition DeathEffect { get; }
        // 이 종류의 생성 요청 중 변환 비율만큼이 나오는 다음 종류의 ID(소행성 → 행성 → 별). 없으면 null이다(BATTLE_COMPOSITION_PLAN 3.4).
        public string UpgradesTo { get; }
        // 노드 밖의 기본 변환 비율(%): 성장도가 BaseUpgradeFromStage 이상이면 이만큼이 변환 대상으로 나온다. 변환 노드는 여기에 더한다.
        // 원작: 성장도 10(이정표)에 닿으면 행성이 기본 3%로 나오기 시작한다 [사용자].
        public float BaseUpgrade { get; }
        public int BaseUpgradeFromStage { get; }
        // 특수 종류이면 부모 종류의 ID. 부모로 정해진 생성 중 이 종류의 생성 확률만큼이 이 종류로 나온다. 없으면 null이다.
        public string SpecialOf { get; }
        public bool IsSpecial => SpecialOf != null;

        public EnemyDefinition(
            string id,
            float moveSpeed,
            IReadOnlyList<EnemyTier> tiers,
            IReadOnlyList<StageColorDefinition> stageColors,
            IReadOnlyList<MassLevelDefinition> massLevels,
            float goldenMultiplier,
            OrbitBehaviorDefinition behavior,
            DeathEffectDefinition deathEffect = null,
            string upgradesTo = null,
            string specialOf = null,
            float baseUpgrade = 0,
            int baseUpgradeFromStage = HqGrowthDefinition.StartStage)
        {
            if (float.IsNaN(goldenMultiplier) || float.IsInfinity(goldenMultiplier) || goldenMultiplier < 0)
                throw new ArgumentOutOfRangeException(nameof(goldenMultiplier), "0 이상의 유한한 값이 필요하다.");

            if (float.IsNaN(baseUpgrade) || baseUpgrade < 0 || baseUpgrade > 100)
                throw new ArgumentOutOfRangeException(nameof(baseUpgrade), "기본 변환 비율은 0부터 100(%)까지다.");

            if (baseUpgrade > 0 && string.IsNullOrEmpty(upgradesTo))
                throw new ArgumentException("변환 대상이 없어 기본 변환 비율을 둘 수 없다.", nameof(baseUpgrade));

            if (baseUpgradeFromStage < HqGrowthDefinition.StartStage)
                throw new ArgumentOutOfRangeException(nameof(baseUpgradeFromStage), $"{HqGrowthDefinition.StartStage} 이상이 필요하다.");

            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("ID가 비어 있다.", nameof(id));

            if (upgradesTo == id)
                throw new ArgumentException("자기 자신으로 변환할 수 없다.", nameof(upgradesTo));

            if (specialOf == id)
                throw new ArgumentException("자기 자신의 특수 종류일 수 없다.", nameof(specialOf));

            if (tiers == null || tiers.Count == 0)
                throw new ArgumentException("색 등급이 하나 이상 필요하다.", nameof(tiers));

            if (massLevels == null || massLevels.Count == 0)
                throw new ArgumentException("질량 단계가 하나 이상 필요하다.", nameof(massLevels));

            for (int i = 0; i < massLevels.Count; i++)
            {
                if (massLevels[i] == null)
                    throw new ArgumentException($"질량 단계 {i}가 null이다.", nameof(massLevels));
            }

            if (stageColors == null || stageColors.Count == 0)
                throw new ArgumentException("성장도별 색 비율이 하나 이상 필요하다.", nameof(stageColors));

            for (int i = 0; i < stageColors.Count; i++)
            {
                if (stageColors[i] == null)
                    throw new ArgumentException($"성장도별 색 비율 {i}가 null이다.", nameof(stageColors));

                if (stageColors[i].TierRatios.Count != tiers.Count)
                    throw new ArgumentException(
                        $"성장도별 색 비율 {i}의 색 비율 수({stageColors[i].TierRatios.Count})가 색 등급 수({tiers.Count})와 다르다.",
                        nameof(stageColors));

                if (i > 0 && stageColors[i].FromStage <= stageColors[i - 1].FromStage)
                    throw new ArgumentException(
                        $"성장도별 색 비율 {i}의 시작 성장도 {stageColors[i].FromStage}는 앞 줄의 {stageColors[i - 1].FromStage}보다 커야 한다.",
                        nameof(stageColors));
            }

            Id = id;
            MoveSpeed = DefinitionGuard.Positive(moveSpeed, nameof(moveSpeed));
            Tiers = Array.AsReadOnly(Copy(tiers));
            StageColors = Array.AsReadOnly(Copy(stageColors));
            MassLevels = Array.AsReadOnly(Copy(massLevels));
            GoldenMultiplier = goldenMultiplier;
            Behavior = behavior ?? throw new ArgumentNullException(nameof(behavior), "행동 정의가 필요하다.");
            DeathEffect = deathEffect;
            UpgradesTo = string.IsNullOrEmpty(upgradesTo) ? null : upgradesTo;
            SpecialOf = string.IsNullOrEmpty(specialOf) ? null : specialOf;
            BaseUpgrade = baseUpgrade;
            BaseUpgradeFromStage = baseUpgradeFromStage;
        }

        // 성장도가 stage일 때 노드 밖의 기본 변환 비율(%).
        public float BaseUpgradeAt(int stage) => stage >= BaseUpgradeFromStage ? BaseUpgrade : 0;

        // 성장도가 stage일 때의 색 비율: FromStage ≤ stage인 마지막 줄. stage가 첫 줄보다 작으면 첫 줄이다.
        public IReadOnlyList<float> TierRatiosAt(int stage)
        {
            StageColorDefinition chosen = StageColors[0];

            foreach (StageColorDefinition row in StageColors)
            {
                if (row.FromStage <= stage)
                    chosen = row;
            }

            return chosen.TierRatios;
        }

        // 판 구성 composition에서 색 등급 tier의 실행 수치.
        // HP = 색의 기본 HP × 질량 단계의 HP 계수, Gold = 색의 기본 Gold × 질량 단계의 Gold 계수(반올림 [임시]),
        // 크기 = 색의 크기, 속도 = 종류의 속도, EXP = 색의 EXP(질량 단계·황금과 무관).
        // 황금이면 Gold에 판 구성의 황금 배율을 한 번 더 곱한다(반올림). HP·크기는 같은 색과 같다 [임시].
        // 판 조립(EnemyStatTable)과 다음 판을 미리 보는 콘솔이 같은 계산을 쓴다.
        public EnemyStats StatsAt(EnemyComposition composition, int tier, bool golden = false)
        {
            if (golden && !CanBeGolden)
                throw new ArgumentException($"'{Id}'는 황금이 되지 않는다.", nameof(golden));

            int massLevel = composition.MassLevel;

            if (massLevel >= MassLevels.Count)
                throw new ArgumentOutOfRangeException(
                    nameof(composition), $"'{Id}'의 질량 단계는 0부터 {MassLevels.Count - 1}까지다. 받은 값: {massLevel}.");

            if (tier < 0 || tier >= Tiers.Count)
                throw new ArgumentOutOfRangeException(
                    nameof(tier), $"'{Id}'의 색 등급은 0부터 {Tiers.Count - 1}까지다. 받은 값: {tier}.");

            MassLevelDefinition level = MassLevels[massLevel];
            EnemyTier row = Tiers[tier];
            long gold = Multiply(row.Gold, level.GoldMultiplier);

            if (golden)
                gold = Multiply(gold, composition.GoldenMultiplier);

            return new EnemyStats(row.MaxHealth * level.HealthMultiplier, MoveSpeed, row.Size, gold, row.Exp);
        }

        private static long Multiply(long gold, float multiplier) =>
            checked((long)Math.Round(gold * (double)multiplier, MidpointRounding.AwayFromZero));

        private static T[] Copy<T>(IReadOnlyList<T> source)
        {
            var copy = new T[source.Count];

            for (int i = 0; i < copy.Length; i++)
                copy[i] = source[i];

            return copy;
        }
    }
}
