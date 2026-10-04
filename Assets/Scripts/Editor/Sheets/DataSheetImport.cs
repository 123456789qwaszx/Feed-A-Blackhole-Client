using System.Collections.Generic;
using System.IO;
using System.Text;
using BlackHole.Authoring;
using BlackHole.Core;
using BlackHole.Unity;
using UnityEditor;
using UnityEngine;

namespace BlackHole.EditorTools
{
    // 데이터 시트와 콘텐츠 에셋 사이의 Unity 쪽. 탭(CSV)을 읽어 검사를 통과하면 에셋에 쓰고, 에셋을 CSV로 낸다.
    //
    // 탭 묶음이 에셋을 채운다: Growth·Milestones → 블랙홀 성장 설정, Skills → 스킬 설정,
    // Enemies·EnemyTiers·EnemyStageColors·EnemyMassLevels·EnemySizeClasses·EnemyTraits → 적 종류 에셋들(ID로 짝짓는다), Supply·StartSupply → 적 공급 설정.
    // Nodes·NodeUpgrades → 노드 목록 에셋의 가격·업그레이드(ID로 짝짓는다. 칸·선·시작 노드는 노드 도구의 것). UpgradeStats 탭은 내보내기만 한다.
    // 가져오기는 받은 탭의 묶음만 다루고, 묶음의 탭이 일부만 오면 오류다.
    // 모두 통과해야 쓴다(부분 통과 금지): 탭마다의 칸·규칙 검사 → 게임 시작(GameBootstrap)과 같은 전체 검사.
    // 그래서 가져오기가 통과했으면 Play도 콘텐츠 오류 없이 시작한다.
    internal static class DataSheetImport
    {
        public static readonly string[] Tabs =
        {
            HqGrowthSheet.LevelsTab, HqGrowthSheet.MilestonesTab, SkillSheet.Tab,
            EnemySheet.EnemiesTab, EnemySheet.TiersTab, EnemySheet.StageColorsTab, EnemySheet.MassLevelsTab, EnemySheet.SizeClassesTab,
            EnemySheet.TraitsTab, SupplySheet.SupplyTab, SupplySheet.StartSupplyTab, NodeSheet.NodesTab, NodeSheet.UpgradesTab,
        };

        // 내보내기만 하는 탭: NodeUpgrades의 stat 열 드롭다운의 원본.
        public const string ReferenceTab = NodeSheet.StatsTab;

        public static string FileOf(string tab) => tab + ".csv";

        public static void Export(ContentAssets assets, string folder)
        {
            HqGrowthData growth = assets.Growth.ToData();
            var skills = new ContentData();
            assets.Skills.WriteTo(skills);
            var supply = new EnemyContentData();
            assets.Supply.WriteTo(supply);
            var enemies = new EnemyContentData();
            assets.Enemies.WriteTo(enemies);
            Dictionary<string, EnemyKind> kinds = KindsById(assets.Enemies);
            string ColorOf(string id, int tier) => HexOf(kinds[id].ColorOf(tier));
            string TraitColorOf(string id, int trait) => HexOf(kinds[id].TraitColorOf(trait));

            Write(folder, HqGrowthSheet.LevelsTab, HqGrowthSheet.LevelsCsv(growth));
            Write(folder, HqGrowthSheet.MilestonesTab, HqGrowthSheet.MilestonesCsv(growth));
            Write(folder, SkillSheet.Tab, SkillSheet.SkillsCsv(skills.Breaker));
            Write(folder, EnemySheet.EnemiesTab, EnemySheet.EnemiesCsv(enemies.Enemies));
            Write(folder, EnemySheet.TiersTab, EnemySheet.TiersCsv(enemies.Enemies, ColorOf));
            Write(folder, EnemySheet.StageColorsTab, EnemySheet.StageColorsCsv(enemies.Enemies));
            Write(folder, EnemySheet.MassLevelsTab, EnemySheet.MassLevelsCsv(enemies.Enemies));
            Write(folder, EnemySheet.SizeClassesTab, EnemySheet.SizeClassesCsv(enemies.Enemies));
            Write(folder, EnemySheet.TraitsTab, EnemySheet.TraitsCsv(enemies.Enemies, TraitColorOf));
            Write(folder, SupplySheet.SupplyTab, SupplySheet.SupplyCsv(supply));
            Write(folder, SupplySheet.StartSupplyTab, SupplySheet.StartSupplyCsv(supply));
            Write(folder, NodeSheet.NodesTab, NodeSheet.NodesCsv(assets.Nodes.ToData()));
            Write(folder, NodeSheet.UpgradesTab, NodeSheet.UpgradesCsv(assets.Nodes.ToData()));
            Write(folder, NodeSheet.StatsTab, NodeSheet.StatsCsv(UpgradeStatNames.For(enemies.Enemies)));
        }

        // 반환: 오류(없으면 통과). written에 바꾼 에셋, unchanged에 값이 같아 두지 않은 에셋의 이름을 더한다.
        // warnings는 가져오기를 막지 않는 알림(값을 줄이는 곱하기)이다.
        public static List<ContentDiagnostic> Import(ContentAssets assets, IReadOnlyDictionary<string, string> csvByTab,
            List<string> written, List<string> unchanged, List<ContentDiagnostic> warnings)
        {
            var errors = new List<ContentDiagnostic>();
            bool growth = Has(csvByTab, errors, HqGrowthSheet.LevelsTab, HqGrowthSheet.MilestonesTab);
            bool skills = Has(csvByTab, errors, SkillSheet.Tab);
            bool enemies = Has(csvByTab, errors, EnemySheet.EnemiesTab, EnemySheet.TiersTab, EnemySheet.StageColorsTab, EnemySheet.MassLevelsTab,
                EnemySheet.SizeClassesTab, EnemySheet.TraitsTab);
            bool supply = Has(csvByTab, errors, SupplySheet.SupplyTab, SupplySheet.StartSupplyTab);
            bool nodes = Has(csvByTab, errors, NodeSheet.NodesTab, NodeSheet.UpgradesTab);

            if (errors.Count == 0 && !growth && !skills && !enemies && !supply && !nodes)
                errors.Add(new ContentDiagnostic(string.Empty, "가져올 탭이 없다."));

            if (errors.Count > 0)
                return errors;

            // 지금 에셋으로 채운 뒤 받은 묶음만 시트 값으로 바꾼다. 받지 않은 묶음은 지금 에셋 값으로 함께 검사한다.
            ContentData data = GameBootstrap.ContentDataFrom(assets.Skills, assets.Enemies, assets.Supply, assets.Growth);

            if (growth)
                errors.AddRange(HqGrowthSheet.Read(csvByTab[HqGrowthSheet.LevelsTab], csvByTab[HqGrowthSheet.MilestonesTab], data));

            if (skills)
                errors.AddRange(SkillSheet.Read(csvByTab[SkillSheet.Tab], data));

            // 공급 검사가 시트의 적 종류로 보도록 적 종류를 먼저 읽는다.
            var colors = new Dictionary<string, List<string>>();
            var traitColors = new Dictionary<string, List<string>>();

            if (enemies)
                errors.AddRange(EnemySheet.Read(csvByTab[EnemySheet.EnemiesTab], csvByTab[EnemySheet.TiersTab],
                    csvByTab[EnemySheet.StageColorsTab], csvByTab[EnemySheet.MassLevelsTab], csvByTab[EnemySheet.SizeClassesTab],
                    csvByTab[EnemySheet.TraitsTab], data, colors, traitColors));

            if (supply)
                errors.AddRange(SupplySheet.Read(csvByTab[SupplySheet.SupplyTab], csvByTab[SupplySheet.StartSupplyTab], data));

            // 노드 목록 에셋을 바꾸지 않고 수치만 시트 값으로 채운 복사본. 쓸 수 있는 수치 이름은 (시트의) 적 종류로 정한다.
            NodeTreeData tree = NodeSheet.Copy(assets.Nodes.Tree);

            if (nodes)
            {
                var stats = new List<string>();

                foreach ((string name, _) in UpgradeStatNames.For(data.Enemies.Enemies))
                    stats.Add(name);

                errors.AddRange(NodeSheet.Read(csvByTab[NodeSheet.NodesTab], csvByTab[NodeSheet.UpgradesTab], tree, stats, warnings));
            }

            if (errors.Count > 0)
                return errors;

            CheckGame(data, tree, errors);

            if (errors.Count > 0)
                return errors;

            var changes = new List<(Object Asset, System.Action Apply)>();

            if (growth)
                Collect(changes, written, unchanged, assets.Growth, GrowthCsv(assets.Growth.ToData()) == GrowthCsv(data.Growth),
                    () => assets.Growth.Replace(data.Growth));

            if (skills)
            {
                var before = new ContentData();
                assets.Skills.WriteTo(before);
                Collect(changes, written, unchanged, assets.Skills,
                    SkillSheet.SkillsCsv(before.Breaker) == SkillSheet.SkillsCsv(data.Breaker),
                    () => assets.Skills.Replace(data.Breaker));
            }

            if (enemies)
                CollectEnemies(changes, written, unchanged, assets.Enemies, data.Enemies.Enemies, colors, traitColors);

            if (supply)
            {
                var before = new EnemyContentData();
                assets.Supply.WriteTo(before);
                List<EnemySupplySetup.Entry> entries = EntriesOf(data.Enemies.StartSupply, assets.Enemies);
                Collect(changes, written, unchanged, assets.Supply, SupplyCsv(before) == SupplyCsv(data.Enemies),
                    () => assets.Supply.Replace(data.Enemies.EnemyPlacement, data.Enemies.PickupPlacement, data.Enemies.MaxAliveEnemies, entries));
            }

            if (nodes)
                Collect(changes, written, unchanged, assets.Nodes, NodesCsv(assets.Nodes.ToData()) == NodesCsv(tree),
                    () => assets.Nodes.ReplaceNumbers(tree));

            if (changes.Count == 0)
                return errors;

            var targets = new List<Object>();

            foreach ((Object asset, _) in changes)
                targets.Add(asset);

            Undo.RecordObjects(targets.ToArray(), "데이터 시트 가져오기");

            foreach ((Object asset, System.Action apply) in changes)
            {
                apply();
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssetIfDirty(asset);
            }

            // 열려 있는 노드 도구가 새 가격·업그레이드를 보이게 한다.
            if (nodes)
                NodeTreeWindow.RefreshOpen();

            return errors;
        }

        // 게임 시작과 같은 순서: 콘텐츠 로드 → 노드 트리 로드 → 노드를 모두 산 경우의 판 조립 가능 여부.
        private static void CheckGame(ContentData data, NodeTreeData nodes, List<ContentDiagnostic> errors)
        {
            ContentLoadResult content = ContentLoader.Load(data);
            AddGame("콘텐츠", content.Diagnostics, errors);

            if (!content.Succeeded)
                return;

            NodeTreeLoadResult tree = NodeTreeLoader.Load(nodes);
            AddGame("노드 트리", tree.Diagnostics, errors);

            if (tree.Succeeded)
                AddGame("노드 × 콘텐츠", UpgradeContentCheck.Check(content.Content, tree.Tree), errors);
        }

        private static void AddGame(string stage, IReadOnlyList<ContentDiagnostic> diagnostics, List<ContentDiagnostic> errors)
        {
            foreach (ContentDiagnostic diagnostic in diagnostics)
                errors.Add(new ContentDiagnostic($"게임 검사({stage}) {diagnostic.Path}", SheetCells.RuleMessage(diagnostic.Message)));
        }

        // 묶음의 탭이 모두 있으면 true, 하나도 없으면 false. 일부만 있으면 오류를 더한다.
        private static bool Has(IReadOnlyDictionary<string, string> csvByTab, List<ContentDiagnostic> errors, params string[] tabs)
        {
            var missing = new List<string>();

            foreach (string tab in tabs)
            {
                if (!csvByTab.ContainsKey(tab))
                    missing.Add(tab);
            }

            if (missing.Count > 0 && missing.Count < tabs.Length)
                errors.Add(new ContentDiagnostic(string.Join(", ", missing), $"{string.Join("·", tabs)} 탭은 함께 가져온다. 이 탭이 없다."));

            return missing.Count == 0;
        }

        private static void Collect(List<(Object, System.Action)> changes, List<string> written, List<string> unchanged,
            Object asset, bool same, System.Action apply)
        {
            if (same)
            {
                unchanged.Add(asset.name);
                return;
            }

            written.Add(asset.name);
            changes.Add((asset, apply));
        }

        // 시트 검사가 적 종류 목록에 있는 ID만 통과시켰으므로 모두 찾는다.
        private static List<EnemySupplySetup.Entry> EntriesOf(List<SupplyData> supply, EnemyCatalog catalog)
        {
            Dictionary<string, EnemyKind> kinds = KindsById(catalog);

            var entries = new List<EnemySupplySetup.Entry>(supply.Count);

            foreach (SupplyData item in supply)
                entries.Add(new EnemySupplySetup.Entry { kind = kinds[item.Enemy], count = item.Count });

            return entries;
        }

        // 종류마다 따로 비교해 바뀐 종류의 에셋만 쓴다. 색은 글자가 같으면 지금 에셋의 Color를 그대로 둔다(float 끝자리가 흔들리지 않게).
        private static void CollectEnemies(List<(Object, System.Action)> changes, List<string> written, List<string> unchanged,
            EnemyCatalog catalog, List<EnemyData> sheet, Dictionary<string, List<string>> colors, Dictionary<string, List<string>> traitColors)
        {
            Dictionary<string, EnemyKind> kinds = KindsById(catalog);

            foreach (EnemyData data in sheet)
            {
                EnemyKind kind = kinds[data.Id];
                List<string> hexes = colors[data.Id];
                List<string> traitHexes = traitColors[data.Id];
                EnemyData before = kind.ToData();
                bool same = EnemyCsv(before, (id, tier) => HexOf(kind.ColorOf(tier)), (id, trait) => HexOf(kind.TraitColorOf(trait)))
                    == EnemyCsv(data, (id, tier) => hexes[tier], (id, trait) => traitHexes[trait]);

                List<Color> resolved = Resolve(hexes, before.Tiers.Count, kind.ColorOf);
                List<Color> resolvedTraits = Resolve(traitHexes, before.Traits.Count, kind.TraitColorOf);
                EnemyKind upgradesTo = data.UpgradesTo != null ? kinds[data.UpgradesTo] : null;
                Collect(changes, written, unchanged, kind, same, () => kind.Replace(data, resolved, resolvedTraits, upgradesTo));
            }
        }

        // 글자가 지금 에셋의 색과 같으면 에셋의 Color를 그대로 두고(float 끝자리가 흔들리지 않게), 다르면 글자를 읽는다.
        private static List<Color> Resolve(List<string> hexes, int existing, System.Func<int, Color> current)
        {
            var resolved = new List<Color>(hexes.Count);

            for (int i = 0; i < hexes.Count; i++)
            {
                if (i < existing && HexOf(current(i)) == hexes[i])
                    resolved.Add(current(i));
                else
                    resolved.Add(ColorUtility.TryParseHtmlString(hexes[i], out Color color) ? color : Color.white);
            }

            return resolved;
        }

        private static Dictionary<string, EnemyKind> KindsById(EnemyCatalog catalog)
        {
            var kinds = new Dictionary<string, EnemyKind>();

            foreach (EnemyKind kind in catalog.Kinds())
                kinds[kind.Id] = kind;

            return kinds;
        }

        // 불투명이면 #RRGGBB, 아니면 #RRGGBBAA.
        private static string HexOf(Color color)
        {
            string rgba = ColorUtility.ToHtmlStringRGBA(color);
            return "#" + (rgba.EndsWith("FF", System.StringComparison.Ordinal) ? rgba.Substring(0, 6) : rgba);
        }

        private static string EnemyCsv(EnemyData data, System.Func<string, int, string> colorOf, System.Func<string, int, string> traitColorOf)
        {
            var one = new List<EnemyData> { data };
            return EnemySheet.EnemiesCsv(one) + EnemySheet.TiersCsv(one, colorOf) + EnemySheet.StageColorsCsv(one) + EnemySheet.MassLevelsCsv(one)
                + EnemySheet.SizeClassesCsv(one) + EnemySheet.TraitsCsv(one, traitColorOf);
        }

        private static string GrowthCsv(HqGrowthData data) => HqGrowthSheet.LevelsCsv(data) + HqGrowthSheet.MilestonesCsv(data);

        private static string SupplyCsv(EnemyContentData data) => SupplySheet.SupplyCsv(data) + SupplySheet.StartSupplyCsv(data);

        private static string NodesCsv(NodeTreeData tree) => NodeSheet.NodesCsv(tree) + NodeSheet.UpgradesCsv(tree);

        private static void Write(string folder, string tab, string csv) =>
            File.WriteAllText(Path.Combine(folder, FileOf(tab)), csv, new UTF8Encoding(false));
    }
}
