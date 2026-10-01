using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // EnemyContentData(저작 형식) → EnemyContent(검증된 정의).
    //
    // 오류가 하나라도 있으면 null을 돌려주고, 모든 진단을 into에 더한다(부분 통과 금지).
    // 여기서 새로 두는 규칙은 데이터 모양에 관한 것뿐이다(빠진 칸, 알 수 없는 종류 이름, 정의되지 않은 참조).
    // 수치 규칙은 정의 생성자를, 콘텐츠 전체 규칙은 EnemyContentInvariants를 그대로 호출해 경로를 붙인다.
    //
    // 세 단계로 읽는다. 앞 단계에 오류가 있으면 뒤 단계를 보지 않는다(잘못된 정의가 거짓 참조 오류를 만들지 않게).
    // 1. 개별 정의: 적 종류(색 등급·성장도별 색 비율·질량 단계·특수 성질과 그 사망 효과), 출현 배치.
    // 2. 적 종류를 가리키는 것: 적 ID 유일, 종류 사이 연결(변환 대상), 공급(픽업 제외), 전체 개체 수 상한.
    // 3. 전체: 전투 시작 공급이 상한 안인가.
    public static class EnemyContentLoader
    {
        public static EnemyContent Load(EnemyContentData data, List<ContentDiagnostic> into)
        {
            if (into == null)
                throw new ArgumentNullException(nameof(into));

            if (data == null)
            {
                into.Add(new ContentDiagnostic(string.Empty, "적 콘텐츠 데이터가 null이다."));
                return null;
            }

            int errors = into.Count;

            List<EnemyDefinition> enemies = LoadEnemies(data.Enemies, into);
            EnemyPlacementDefinition placement = LoadPlacement(data.EnemyPlacement, into);

            if (into.Count > errors)
                return null;

            EnemyContentInvariants.CollectEnemies(enemies, into, out Dictionary<string, EnemyDefinition> enemiesById);
            EnemyContentInvariants.CheckKindLinks(enemies, enemiesById, into);
            List<SupplyRequest> startSupply = LoadSupplyList(data.StartSupply, "StartSupply", enemiesById, into);
            EnemyContentInvariants.CheckSupplyKinds(startSupply, "StartSupply", into);

            if (startSupply.Count > 0 && placement == null)
                into.Add(new ContentDiagnostic("EnemyPlacement", "공급이 있으면 출현 배치가 필요하다."));

            EnemyContentInvariants.CheckMaxAlive(placement, data.MaxAliveEnemies, into);

            if (into.Count > errors)
                return null;

            EnemyContentInvariants.CheckStartSupplyFits(startSupply, 0, data.MaxAliveEnemies, into);

            if (into.Count > errors)
                return null;

            return new EnemyContent(enemies, placement, data.MaxAliveEnemies, startSupply);
        }

        private static List<EnemyDefinition> LoadEnemies(List<EnemyData> items, List<ContentDiagnostic> into)
        {
            var enemies = new List<EnemyDefinition>();

            if (items == null)
                return enemies;

            for (int i = 0; i < items.Count; i++)
            {
                EnemyData item = items[i];
                string at = At("Enemies", i, item?.Id);

                if (item == null)
                {
                    into.Add(new ContentDiagnostic(at, "적 데이터가 null이다."));
                    continue;
                }

                int errors = into.Count;
                List<EnemyTraitDefinition> traits = LoadTraits(item.Traits, at + ".Traits", into);
                List<EnemyTier> tiers = LoadTiers(item.Tiers, at + ".Tiers", into);
                List<StageColorDefinition> stageColors = LoadStageColors(item.StageColors, at + ".StageColors", into);
                List<MassLevelDefinition> massLevels = LoadMassLevels(item.MassLevels, at + ".MassLevels", into);
                List<SizeClassDefinition> sizeClasses = LoadSizeClasses(item.SizeClasses, at + ".SizeClasses", into);

                if (into.Count > errors)
                    continue;

                EnemyDefinition enemy = Guard(at, into, () =>
                    new EnemyDefinition(item.Id, item.MoveSpeed, tiers, stageColors, massLevels, traits, item.UpgradesTo,
                        item.BaseUpgrade, item.BaseUpgradeFromStage, sizeClasses, item.PickupPeriod));

                if (enemy != null)
                    enemies.Add(enemy);
            }

            return enemies;
        }

        // 줄마다 수치를 검사한다. 줄 수(하나 이상)와 성장도별 색 비율과의 길이 맞춤은 EnemyDefinition이 검사한다.
        private static List<EnemyTier> LoadTiers(List<EnemyTierData> items, string at, List<ContentDiagnostic> into)
        {
            var tiers = new List<EnemyTier>();

            for (int i = 0; items != null && i < items.Count; i++)
            {
                EnemyTierData item = items[i];

                if (item == null)
                {
                    into.Add(new ContentDiagnostic($"{at}[{i}]", "데이터가 없다."));
                    continue;
                }

                EnemyTier? tier = GuardValue($"{at}[{i}]", into, () => new EnemyTier(item.MaxHealth, item.Size, item.Gold, item.Exp));

                if (tier.HasValue)
                    tiers.Add(tier.Value);
            }

            return tiers;
        }

        private static List<MassLevelDefinition> LoadMassLevels(List<MassLevelData> items, string at, List<ContentDiagnostic> into)
        {
            var levels = new List<MassLevelDefinition>();

            for (int i = 0; items != null && i < items.Count; i++)
            {
                MassLevelData item = items[i];

                if (item == null)
                {
                    into.Add(new ContentDiagnostic($"{at}[{i}]", "데이터가 없다."));
                    continue;
                }

                MassLevelDefinition level = Guard($"{at}[{i}]", into,
                    () => new MassLevelDefinition(item.HealthMultiplier, item.GoldMultiplier));

                if (level != null)
                    levels.Add(level);
            }

            return levels;
        }

        // 비어 있으면 크기 등급이 없는 종류다(EnemyDefinition이 모든 계수 1인 한 줄로 둔다).
        private static List<SizeClassDefinition> LoadSizeClasses(List<SizeClassData> items, string at, List<ContentDiagnostic> into)
        {
            var classes = new List<SizeClassDefinition>();

            for (int i = 0; items != null && i < items.Count; i++)
            {
                SizeClassData item = items[i];

                if (item == null)
                {
                    into.Add(new ContentDiagnostic($"{at}[{i}]", "데이터가 없다."));
                    continue;
                }

                SizeClassDefinition size = Guard($"{at}[{i}]", into,
                    () => new SizeClassDefinition(item.SizeMultiplier, item.HealthMultiplier, item.GoldMultiplier, item.ExpMultiplier));

                if (size != null)
                    classes.Add(size);
            }

            return classes;
        }

        // 줄마다 시작 성장도와 색 비율을 검사한다. 줄 수·순서·색 등급과의 길이 맞춤은 EnemyDefinition이 검사한다.
        private static List<StageColorDefinition> LoadStageColors(List<StageColorData> items, string at, List<ContentDiagnostic> into)
        {
            var rows = new List<StageColorDefinition>();

            for (int i = 0; items != null && i < items.Count; i++)
            {
                StageColorData item = items[i];

                if (item == null)
                {
                    into.Add(new ContentDiagnostic($"{at}[{i}]", "데이터가 없다."));
                    continue;
                }

                StageColorDefinition row = Guard($"{at}[{i}]", into, () => new StageColorDefinition(item.FromStage, item.TierRatios));

                if (row != null)
                    rows.Add(row);
            }

            return rows;
        }

        // 줄마다 ID와 효과를 검사한다. ID 유일·픽업의 성질 수는 EnemyDefinition이 검사한다.
        private static List<EnemyTraitDefinition> LoadTraits(List<EnemyTraitData> items, string at, List<ContentDiagnostic> into)
        {
            var traits = new List<EnemyTraitDefinition>();

            for (int i = 0; items != null && i < items.Count; i++)
            {
                EnemyTraitData item = items[i];
                string itemAt = At(at, i, item?.Id);

                if (item == null)
                {
                    into.Add(new ContentDiagnostic(itemAt, "데이터가 없다."));
                    continue;
                }

                int errors = into.Count;
                DeathEffectDefinition effect = LoadDeathEffect(item.Effect, itemAt + ".Effect", into);

                if (into.Count > errors)
                    continue;

                EnemyTraitDefinition trait = Guard(itemAt, into, () => new EnemyTraitDefinition(item.Id, effect));

                if (trait != null)
                    traits.Add(trait);
            }

            return traits;
        }

        // 종류 이름을 하위 정의로 바꾸고, 가능한 값을 진단에 싣는다. 성질에는 효과가 있어야 하므로 비어 있으면 오류다.
        public static readonly string[] DeathEffectKinds = { "Golden", "ChainLightning", "Explosion", "LaserBurst", "MoonBuff", "CometBuff" };

        private static DeathEffectDefinition LoadDeathEffect(DeathEffectData item, string at, List<ContentDiagnostic> into)
        {
            if (item == null || string.IsNullOrEmpty(item.Kind))
            {
                into.Add(new ContentDiagnostic(at + ".Kind", $"성질의 사망 효과가 비어 있다. 가능한 값: {string.Join(", ", DeathEffectKinds)}."));
                return null;
            }

            switch (item.Kind)
            {
                case "Golden":
                    return Guard(at, into, () => new GoldenDefinition(item.Multiplier));
                case "ChainLightning":
                    return Guard(at, into, () => new ChainLightningDefinition(
                        item.Damage, item.Radius, item.MaxTargets, item.BranchChance, item.CritChance, item.CritMultiplier));
                case "Explosion":
                    return Guard(at, into, () => new ExplosionDefinition(item.HealthFraction, item.Radius));
                case "LaserBurst":
                    return Guard(at, into, () => new LaserBurstDefinition(item.Damage, item.Width, item.CritChance, item.CritMultiplier));
                case "MoonBuff":
                    return Guard(at, into, () => new MoonBuffDefinition());
                case "CometBuff":
                    return Guard(at, into, () => new CometBuffDefinition());
                default:
                    into.Add(new ContentDiagnostic(at + ".Kind",
                        $"알 수 없는 사망 효과 종류 '{item.Kind}'. 가능한 값: {string.Join(", ", DeathEffectKinds)}."));
                    return null;
            }
        }

        // 없으면 null이다. 공급이 있을 때만 필요하다(Load에서 본다).
        private static EnemyPlacementDefinition LoadPlacement(EnemyPlacementData item, List<ContentDiagnostic> into)
        {
            if (item == null)
                return null;

            return Guard("EnemyPlacement", into, () => new EnemyPlacementDefinition(item.MinDistance, item.MaxDistance));
        }

        private static List<SupplyRequest> LoadSupplyList(
            List<SupplyData> items,
            string section,
            IReadOnlyDictionary<string, EnemyDefinition> enemies,
            List<ContentDiagnostic> into)
        {
            var requests = new List<SupplyRequest>();

            if (items == null)
                return requests;

            for (int i = 0; i < items.Count; i++)
            {
                SupplyData item = items[i];
                string at = $"{section}[{i}]";

                if (item == null)
                {
                    into.Add(new ContentDiagnostic(at, "공급 데이터가 null이다."));
                    continue;
                }

                if (item.Enemy == null || !enemies.TryGetValue(item.Enemy, out EnemyDefinition enemy))
                {
                    into.Add(new ContentDiagnostic(at + ".Enemy", $"정의되지 않은 적 ID '{item.Enemy}'."));
                    continue;
                }

                SupplyRequest? request = GuardValue(at, into, () => new SupplyRequest(enemy, item.Count));

                if (request.HasValue)
                    requests.Add(request.Value);
            }

            return requests;
        }

        // ── 공통 ────────────────────────────────────────────────────────────

        // 정의 생성자의 규칙 위반을 그 자리의 진단으로 바꾼다.
        private static T Guard<T>(string at, List<ContentDiagnostic> into, Func<T> create) where T : class
        {
            try { return create(); }
            catch (ArgumentException error)
            {
                into.Add(new ContentDiagnostic(at, error.Message));
                return null;
            }
        }

        private static T? GuardValue<T>(string at, List<ContentDiagnostic> into, Func<T> create) where T : struct
        {
            try { return create(); }
            catch (ArgumentException error)
            {
                into.Add(new ContentDiagnostic(at, error.Message));
                return null;
            }
        }

        private static string At(string section, int index, string id) =>
            string.IsNullOrWhiteSpace(id) ? $"{section}[{index}]" : $"{section}[{id}]";
    }
}
