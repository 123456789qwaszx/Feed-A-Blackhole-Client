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
    // 탭 묶음 하나가 에셋 하나를 채운다: Growth·Milestones → 블랙홀 성장 설정, Skills → 스킬 설정, Supply·StartSupply → 적 공급 설정.
    // 가져오기는 받은 탭의 묶음만 다루고, 묶음의 탭이 일부만 오면 오류다.
    // 모두 통과해야 쓴다(부분 통과 금지): 탭마다의 칸·규칙 검사 → 게임 시작(GameBootstrap)과 같은 전체 검사.
    // 그래서 가져오기가 통과했으면 Play도 콘텐츠 오류 없이 시작한다.
    internal static class DataSheetImport
    {
        public static readonly string[] Tabs =
        {
            HqGrowthSheet.StagesTab, HqGrowthSheet.MilestonesTab, SkillSheet.Tab, SupplySheet.SupplyTab, SupplySheet.StartSupplyTab,
        };

        public static string FileOf(string tab) => tab + ".csv";

        public static void Export(ContentAssets assets, string folder)
        {
            HqGrowthData growth = assets.Growth.ToData();
            var skills = new ContentData();
            assets.Skills.WriteTo(skills);
            var supply = new EnemyContentData();
            assets.Supply.WriteTo(supply);

            Write(folder, HqGrowthSheet.StagesTab, HqGrowthSheet.StagesCsv(growth));
            Write(folder, HqGrowthSheet.MilestonesTab, HqGrowthSheet.MilestonesCsv(growth));
            Write(folder, SkillSheet.Tab, SkillSheet.SkillsCsv(skills.Breaker, skills.Laser));
            Write(folder, SupplySheet.SupplyTab, SupplySheet.SupplyCsv(supply));
            Write(folder, SupplySheet.StartSupplyTab, SupplySheet.StartSupplyCsv(supply));
        }

        // 반환: 오류(없으면 통과). written에 바꾼 에셋, unchanged에 값이 같아 두지 않은 에셋의 이름을 더한다.
        public static List<ContentDiagnostic> Import(ContentAssets assets, IReadOnlyDictionary<string, string> csvByTab,
            List<string> written, List<string> unchanged)
        {
            var errors = new List<ContentDiagnostic>();
            bool growth = Has(csvByTab, errors, HqGrowthSheet.StagesTab, HqGrowthSheet.MilestonesTab);
            bool skills = Has(csvByTab, errors, SkillSheet.Tab);
            bool supply = Has(csvByTab, errors, SupplySheet.SupplyTab, SupplySheet.StartSupplyTab);

            if (errors.Count == 0 && !growth && !skills && !supply)
                errors.Add(new ContentDiagnostic(string.Empty, "가져올 탭이 없다."));

            if (errors.Count > 0)
                return errors;

            // 지금 에셋으로 채운 뒤 받은 묶음만 시트 값으로 바꾼다. 받지 않은 묶음은 지금 에셋 값으로 함께 검사한다.
            ContentData data = GameBootstrap.ContentDataFrom(assets.Skills, assets.Enemies, assets.Supply, assets.Growth);

            if (growth)
                errors.AddRange(HqGrowthSheet.Read(csvByTab[HqGrowthSheet.StagesTab], csvByTab[HqGrowthSheet.MilestonesTab], data));

            if (skills)
                errors.AddRange(SkillSheet.Read(csvByTab[SkillSheet.Tab], data));

            if (supply)
                errors.AddRange(SupplySheet.Read(csvByTab[SupplySheet.SupplyTab], csvByTab[SupplySheet.StartSupplyTab], data));

            if (errors.Count > 0)
                return errors;

            CheckGame(data, assets.Nodes, errors);

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
                    SkillSheet.SkillsCsv(before.Breaker, before.Laser) == SkillSheet.SkillsCsv(data.Breaker, data.Laser),
                    () => assets.Skills.Replace(data.Breaker, data.Laser));
            }

            if (supply)
            {
                var before = new EnemyContentData();
                assets.Supply.WriteTo(before);
                List<EnemySupplySetup.Entry> entries = EntriesOf(data.Enemies.StartSupply, assets.Enemies);
                Collect(changes, written, unchanged, assets.Supply, SupplyCsv(before) == SupplyCsv(data.Enemies),
                    () => assets.Supply.Replace(data.Enemies.EnemyPlacement, data.Enemies.MaxAliveEnemies, entries));
            }

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

            return errors;
        }

        // 게임 시작과 같은 순서: 콘텐츠 로드 → 노드 트리 로드 → 노드를 모두 산 경우의 판 조립 가능 여부.
        private static void CheckGame(ContentData data, NodeCatalog nodes, List<ContentDiagnostic> errors)
        {
            ContentLoadResult content = ContentLoader.Load(data);
            AddGame("콘텐츠", content.Diagnostics, errors);

            if (!content.Succeeded)
                return;

            NodeTreeLoadResult tree = NodeTreeLoader.Load(nodes.ToData());
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
            var kinds = new Dictionary<string, EnemyKind>();

            foreach (EnemyKind kind in catalog.Kinds())
                kinds[kind.Id] = kind;

            var entries = new List<EnemySupplySetup.Entry>(supply.Count);

            foreach (SupplyData item in supply)
                entries.Add(new EnemySupplySetup.Entry { kind = kinds[item.Enemy], count = item.Count });

            return entries;
        }

        private static string GrowthCsv(HqGrowthData data) => HqGrowthSheet.StagesCsv(data) + HqGrowthSheet.MilestonesCsv(data);

        private static string SupplyCsv(EnemyContentData data) => SupplySheet.SupplyCsv(data) + SupplySheet.StartSupplyCsv(data);

        private static void Write(string folder, string tab, string csv) =>
            File.WriteAllText(Path.Combine(folder, FileOf(tab)), csv, new UTF8Encoding(false));
    }
}
