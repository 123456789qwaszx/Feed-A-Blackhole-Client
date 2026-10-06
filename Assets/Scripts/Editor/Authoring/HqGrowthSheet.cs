using System;
using System.Collections.Generic;
using BlackHole.Core;
using static BlackHole.Authoring.SheetCells;

namespace BlackHole.Authoring
{
    // 블랙홀 성장(ContentData.Growth)과 데이터 시트의 두 탭 사이의 변환.
    // - Growth 탭: level | exp. 한 행이 Level 하나이고, exp는 그 Level에 닿는 누적 EXP다. Level은 1부터 차례로 둔다.
    // - Milestones 탭: level | targetGold. 판이 그 Level에 닿으면 판이 끝나고 결산이 잔액을 targetGold까지 채운다.
    // 읽을 때는 칸의 모양(정수인가, 차례가 맞는가)을 먼저 보고, 통과하면 게임과 같은 로더(HqGrowthLoader)로 규칙을 본다.
    public static class HqGrowthSheet
    {
        public const string LevelsTab = "Growth";
        public const string MilestonesTab = "Milestones";

        private const int ExpColumn = 1;

        public static string LevelsCsv(HqGrowthData data)
        {
            var rows = new List<IReadOnlyList<string>> { new[] { "level", "exp" } };

            for (int i = 0; i < data.LevelExp.Count; i++)
                rows.Add(new[] { Number(i + 1), Number(data.LevelExp[i]) });

            return Csv.Write(rows);
        }

        public static string MilestonesCsv(HqGrowthData data)
        {
            var rows = new List<IReadOnlyList<string>> { new[] { "level", "targetGold" } };

            foreach (HqMilestoneData mark in data.Milestones)
                rows.Add(new[] { Number(mark.Level), Number(mark.TargetGold) });

            return Csv.Write(rows);
        }

        // 두 탭을 읽어 통과하면 into.Growth를 바꾼다. 반환: 진단(없으면 통과).
        public static List<ContentDiagnostic> Read(string levelsCsv, string milestonesCsv, ContentData into)
        {
            var diagnostics = new List<ContentDiagnostic>();
            var data = new HqGrowthData();
            List<int> levelRows = ReadLevels(Csv.Parse(levelsCsv ?? string.Empty), data.LevelExp, diagnostics);
            List<int> milestoneRows = ReadMilestones(Csv.Parse(milestonesCsv ?? string.Empty), data.Milestones, diagnostics);

            if (diagnostics.Count > 0)
                return diagnostics;

            var rules = new List<ContentDiagnostic>();
            HqGrowthLoader.Load(data, rules);

            foreach (ContentDiagnostic rule in rules)
                diagnostics.Add(new ContentDiagnostic(PlaceOf(rule.Path, levelRows, milestoneRows), RuleMessage(rule.Message)));

            if (diagnostics.Count == 0)
                into.Growth = data;

            return diagnostics;
        }

        // 반환: Level마다의 시트 행 번호.
        private static List<int> ReadLevels(List<string[]> rows, List<long> into, List<ContentDiagnostic> diagnostics)
        {
            var sheetRows = new List<int>();

            if (!HasHeader(rows, LevelsTab, new[] { "level", "exp" }, diagnostics))
                return sheetRows;

            for (int r = 1; r < rows.Count; r++)
            {
                string[] row = rows[r];
                int sheetRow = r + 1;

                if (IsBlank(row, ExpColumn))
                    continue;

                int before = diagnostics.Count;
                int level = ReadInt(row, 0, LevelsTab, sheetRow, diagnostics);

                if (diagnostics.Count == before && level != into.Count + 1)
                    diagnostics.Add(new ContentDiagnostic(Cell(LevelsTab, 0, sheetRow), $"Level은 1부터 차례로 적는다. 여기는 {into.Count + 1}이어야 한다."));

                into.Add(ReadLong(row, ExpColumn, LevelsTab, sheetRow, diagnostics));
                sheetRows.Add(sheetRow);
            }

            return sheetRows;
        }

        private static List<int> ReadMilestones(List<string[]> rows, List<HqMilestoneData> into, List<ContentDiagnostic> diagnostics)
        {
            var sheetRows = new List<int>();

            if (!HasHeader(rows, MilestonesTab, new[] { "level", "targetGold" }, diagnostics))
                return sheetRows;

            for (int r = 1; r < rows.Count; r++)
            {
                string[] row = rows[r];
                int sheetRow = r + 1;

                if (IsBlank(row, 1))
                    continue;

                into.Add(new HqMilestoneData
                {
                    Level = ReadInt(row, 0, MilestonesTab, sheetRow, diagnostics),
                    TargetGold = ReadLong(row, 1, MilestonesTab, sheetRow, diagnostics),
                });
                sheetRows.Add(sheetRow);
            }

            return sheetRows;
        }

        // 로더의 경로("Growth.Milestones[1]", "Growth.LevelExp")를 시트 위치로 바꾼다.
        private static string PlaceOf(string path, List<int> levelRows, List<int> milestoneRows)
        {
            if (TryIndex(path, "Growth.Milestones[", out int mark, out _) && mark < milestoneRows.Count)
                return Range(MilestonesTab, 0, 1, milestoneRows[mark]);

            if (path.StartsWith("Growth.Milestones", StringComparison.Ordinal))
                return MilestonesTab;

            // Level 사다리의 규칙은 어느 Level인지 문장에 있다. exp 열 전체를 가리킨다.
            return levelRows.Count > 0
                ? $"{Cell(LevelsTab, ExpColumn, levelRows[0])}:{Column(ExpColumn)}{levelRows[levelRows.Count - 1]}"
                : LevelsTab;
        }
    }
}
