using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // (색이 다르더라도.)
    // 적 한 종류의 정의:
    // - 이동 속도(부호는 공전 방향),
    // - 색 등급 표,
    // - 성장도별 색 비율,
    // - 질량 단계 표,
    // - 크기 등급 표,
    // - 붙을 수 있는 특수 성질(황금·전기·달·레이저·슈퍼노바 …),
    // - 픽업이면 등장 주기
    public sealed class EnemyDefinition
    {
        public string Id { get; }

        // 공전 속도(초당 이동 거리). 0이 아닌 값이고, 부호가 공전 방향이다: 양수는 반시계, 음수는 시계방향.
        public float MoveSpeed { get; }

        // 색 등급 표. 번호가 적의 색 등급(Enemy.Tier)이다.
        public IReadOnlyList<EnemyTier> Tiers { get; }

        // 블랙홀 성장도별 색 비율. FromStage가 커지는 순서다. 하나 이상.
        public IReadOnlyList<StageColorDefinition> StageColors { get; }

        // 질량 단계 표(HP·Gold 계수). MassLevels[i]가 질량 단계 i다(0 = 질량 증가를 사지 않음).
        public IReadOnlyList<MassLevelDefinition> MassLevels { get; }

        // 크기 등급 표(크기·HP·Gold·EXP 계수). SizeClasses[i]가 크기 등급 i다(0 = 크기 노드를 사지 않아도 나옴).
        // 크기 등급이 없는 종류는 모든 계수가 1인 한 줄이다(SizeClassDefinition.Base).
        public IReadOnlyList<SizeClassDefinition> SizeClasses { get; }

        // 이 종류에 붙을 수 있는 특수 성질(ID 유일). 출현 때 성질마다의 생성 확률(판 구성, 기본 0%)로 최대 하나가 붙는다.
        // 픽업은 성질이 정확히 하나이고 언제나 붙는다(혜성 = 혜성 버프).
        public IReadOnlyList<EnemyTraitDefinition> Traits { get; }

        // 이 종류의 생성 요청 중 변환 비율만큼이 나오는 다음 종류의 ID(소행성 → 행성 → 별).
        public string UpgradesTo { get; }

        // 특정 성장도 도달 시, 고급 적의 출현 보장:
        // - 성장도가 BaseUpgradeFromStage 이상이면 적용 시작.
        // e.g.) 성장도 10(이정표)에 도달 시, 행성이 기본 3%로 나오기 시작.
        public float BaseUpgrade { get; }

        public int BaseUpgradeFromStage { get; }

        // 픽업의 등장 판정 주기(초). 0이면 공급되는 보통 종류다.
        // 픽업(혜성)은 적 공급·성장 공급·변환·전체 개체 수 상한과 무관하다: 주기마다 등장 확률(판 구성의 AppearChance)로 하나가 나온다.
        // 브레이커로 쳐서 획득하는 것이며, 사망 효과의 피해를 받지 않는다(성질이 언제나 붙으므로).
        public float PickupPeriod { get; }

        public bool IsPickup => PickupPeriod > 0;

        public EnemyDefinition(
            string id,
            float moveSpeed,
            IReadOnlyList<EnemyTier> tiers,
            IReadOnlyList<StageColorDefinition> stageColors,
            IReadOnlyList<MassLevelDefinition> massLevels,
            IReadOnlyList<EnemyTraitDefinition> traits = null,
            string upgradesTo = null,
            float baseUpgrade = 0,
            int baseUpgradeFromStage = HqGrowthDefinition.StartStage,
            IReadOnlyList<SizeClassDefinition> sizeClasses = null,
            float pickupPeriod = 0)
        {
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

            if (float.IsNaN(pickupPeriod) || float.IsInfinity(pickupPeriod) || pickupPeriod < 0)
                throw new ArgumentOutOfRangeException(nameof(pickupPeriod), "0 이상의 유한한 값이 필요하다(0 = 픽업이 아님).");

            if (tiers == null || tiers.Count == 0)
                throw new ArgumentException("색 등급이 하나 이상 필요하다.", nameof(tiers));

            if (massLevels == null || massLevels.Count == 0)
                throw new ArgumentException("질량 단계가 하나 이상 필요하다.", nameof(massLevels));

            for (int i = 0; i < massLevels.Count; i++)
            {
                if (massLevels[i] == null)
                    throw new ArgumentException($"질량 단계 {i}가 null이다.", nameof(massLevels));
            }

            for (int i = 0; sizeClasses != null && i < sizeClasses.Count; i++)
            {
                if (sizeClasses[i] == null)
                    throw new ArgumentException($"크기 등급 {i}가 null이다.", nameof(sizeClasses));
            }

            var traitIds = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; traits != null && i < traits.Count; i++)
            {
                if (traits[i] == null)
                    throw new ArgumentException($"성질 {i}가 null이다.", nameof(traits));

                if (!traitIds.Add(traits[i].Id))
                    throw new ArgumentException($"성질 ID '{traits[i].Id}'가 중복됐다.", nameof(traits));
            }

            if (pickupPeriod > 0)
            {
                if (traits == null || traits.Count != 1)
                    throw new ArgumentException("픽업은 성질이 정확히 하나여야 한다(언제나 붙는 효과).", nameof(traits));

                if (!string.IsNullOrEmpty(upgradesTo))
                    throw new ArgumentException("픽업은 변환 대상을 가질 수 없다.", nameof(upgradesTo));
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
            MoveSpeed = DefinitionGuard.NonZeroFinite(moveSpeed, nameof(moveSpeed));
            Tiers = Array.AsReadOnly(Copy(tiers));
            StageColors = Array.AsReadOnly(Copy(stageColors));
            MassLevels = Array.AsReadOnly(Copy(massLevels));
            SizeClasses = sizeClasses == null || sizeClasses.Count == 0
                ? Array.AsReadOnly(new[] { SizeClassDefinition.Base })
                : Array.AsReadOnly(Copy(sizeClasses));
            Traits = traits == null ? Array.AsReadOnly(Array.Empty<EnemyTraitDefinition>()) : Array.AsReadOnly(Copy(traits));
            UpgradesTo = string.IsNullOrEmpty(upgradesTo) ? null : upgradesTo;
            BaseUpgrade = baseUpgrade;
            BaseUpgradeFromStage = baseUpgradeFromStage;
            PickupPeriod = pickupPeriod;
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

        // 판 구성 composition에서 색 등급 tier·크기 등급 sizeClass·성질 trait(없으면 null)의 실행 수치.
        // HP = 색의 기본 HP × 질량 단계의 HP 계수 × 크기 등급의 HP 계수,
        // Gold = 색의 기본 Gold × 질량 단계의 Gold 계수 × 크기 등급의 Gold 계수(반올림 [임시]),
        // 크기 = 색의 크기 × 크기 등급의 크기 계수, 속도 = 종류의 속도,
        // EXP = 색의 EXP × 크기 등급의 EXP 계수(반올림, 질량 단계·성질과 무관).
        // 성질이 황금이면 Gold에 판 구성의 황금 배율을 한 번 더 곱한다(반올림). 다른 성질은 수치를 바꾸지 않는다.
        // trait는 판 구성의 성질(composition.Traits)이어야 한다 — 노드가 반영된 배율이 거기 있다.
        // 판 조립(EnemyStatTable)과 다음 판을 미리 보는 콘솔이 같은 계산을 쓴다.
        public EnemyStats StatsAt(EnemyComposition composition, int tier, EnemyTraitDefinition trait = null, int sizeClass = 0)
        {
            if (trait != null && composition.IndexOfTrait(trait) < 0)
                throw new ArgumentException($"'{trait.Id}'는 이 판 구성에서 '{Id}'의 성질이 아니다.", nameof(trait));

            int massLevel = composition.MassLevel;

            if (massLevel >= MassLevels.Count)
                throw new ArgumentOutOfRangeException(
                    nameof(composition), $"'{Id}'의 질량 단계는 0부터 {MassLevels.Count - 1}까지다. 받은 값: {massLevel}.");

            if (tier < 0 || tier >= Tiers.Count)
                throw new ArgumentOutOfRangeException(
                    nameof(tier), $"'{Id}'의 색 등급은 0부터 {Tiers.Count - 1}까지다. 받은 값: {tier}.");

            if (sizeClass < 0 || sizeClass >= SizeClasses.Count)
                throw new ArgumentOutOfRangeException(
                    nameof(sizeClass), $"'{Id}'의 크기 등급은 0부터 {SizeClasses.Count - 1}까지다. 받은 값: {sizeClass}.");

            MassLevelDefinition level = MassLevels[massLevel];
            SizeClassDefinition size = SizeClasses[sizeClass];
            EnemyTier row = Tiers[tier];
            long gold = Multiply(row.Gold, (double)level.GoldMultiplier * size.GoldMultiplier);

            if (trait?.Effect is GoldenDefinition golden)
                gold = Multiply(gold, golden.Multiplier);

            return new EnemyStats(
                row.MaxHealth * level.HealthMultiplier * size.HealthMultiplier,
                MoveSpeed,
                row.Size * size.SizeMultiplier,
                gold,
                Multiply(row.Exp, size.ExpMultiplier));
        }

        private static long Multiply(long value, double multiplier) =>
            checked((long)Math.Round(value * multiplier, MidpointRounding.AwayFromZero));

        private static T[] Copy<T>(IReadOnlyList<T> source)
        {
            var copy = new T[source.Count];

            for (int i = 0; i < copy.Length; i++)
                copy[i] = source[i];

            return copy;
        }
    }
}
