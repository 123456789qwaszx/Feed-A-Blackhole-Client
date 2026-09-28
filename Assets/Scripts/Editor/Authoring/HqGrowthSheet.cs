using System;
using System.Collections.Generic;
using BlackHole.Core;
using static BlackHole.Authoring.SheetCells;

namespace BlackHole.Authoring
{
    // 블랙홀 성장(ContentData.Growth)과 데이터 시트의 두 탭 사이의 변환.
    // - Growth 탭: stage | goalLevel | 1 | 2 | … 한 행이 성장도 하나이고, 숫자 열은 그 Level에 닿는 누적 EXP다.
    //   숫자 열은 1부터 차례로 둔다. 숫자가 아닌 머리칸이 나오면 그 뒤 열은 읽지 않는다(메모·보조 수식 자리).
    //   한 행의 EXP는 왼쪽부터 채우고 빈 칸에서 끝난다.
    // - Milestones 탭: stage | reward.
    // 읽을 때는 칸의 모양(정수인가, 차례가 맞는가)을 먼저 보고, 통과하면 게임과 같은 로더(HqGrowthLoader)로 규칙을 본다.
    public static class HqGrowthSheet
    {
        public const string StagesTab = "Growth";
        public const string MilestonesTab = "Milestones";

        private const int FirstLevelColumn = 2;

        public static string StagesCsv(HqGrowthData data)
        {
            int levels = 0;

            foreach (GrowthStageData stage in data.Stages)
                levels = Math.Max(levels, stage.LevelExp?.Count ?? 0);

            var header = new List<string> { "stage", "goalLevel" };

            for (int level = 1; level <= levels; level++)
                header.Add(Number(level));

            var rows = new List<IReadOnlyList<string>> { header };

            for (int i = 0; i < data.Stages.Count; i++)
            {
                GrowthStageData stage = data.Stages[i];
                var row = new List<string> { Number(i), Number(stage.GoalLevel) };

                for (int k = 0; k < levels; k++)
                    row.Add(stage.LevelExp != null && k < stage.LevelExp.Count ? Number(stage.LevelExp[k]) : string.Empty);

                rows.Add(row);
            }

            return Csv.Write(rows);
        }

        public static string MilestonesCsv(HqGrowthData data)
        {
            var rows = new List<IReadOnlyList<string>> { new[] { "stage", "reward" } };

            foreach (HqMilestoneData mark in data.Milestones)
                rows.Add(new[] { Number(mark.Stage), Number(mark.Reward) });

            return Csv.Write(rows);
        }

        // 두 탭을 읽어 통과하면 into.Growth를 바꾼다. 반환: 진단(없으면 통과).
        public static List<ContentDiagnostic> Read(string stagesCsv, string milestonesCsv, ContentData into)
        {
            var diagnostics = new List<ContentDiagnostic>();
            var data = new HqGrowthData();
            List<int> stageRows = ReadStages(Csv.Parse(stagesCsv ?? string.Empty), data.Stages, diagnostics, out int lastLevelColumn);
            List<int> milestoneRows = ReadMilestones(Csv.Parse(milestonesCsv ?? string.Empty), data.Milestones, diagnostics);

            if (diagnostics.Count > 0)
                return diagnostics;

            var rules = new List<ContentDiagnostic>();
            HqGrowthLoader.Load(data, rules);

            foreach (ContentDiagnostic rule in rules)
                diagnostics.Add(new ContentDiagnostic(PlaceOf(rule.Path, stageRows, milestoneRows, lastLevelColumn), RuleMessage(rule.Message)));

            if (diagnostics.Count == 0)
                into.Growth = data;

            return diagnostics;
        }

        // 반환: 성장도마다의 시트 행 번호.
        private static List<int> ReadStages(List<string[]> rows, List<GrowthStageData> into, List<ContentDiagnostic> diagnostics,
            out int lastLevelColumn)
        {
            var sheetRows = new List<int>();
            lastLevelColumn = FirstLevelColumn - 1;

            if (!HasHeader(rows, StagesTab, new[] { "stage", "goalLevel" }, diagnostics))
                return sheetRows;

            string[] header = rows[0];

            for (int c = FirstLevelColumn; c < header.Length; c++)
            {
                int expected = c - FirstLevelColumn + 1;

                if (!int.TryParse(header[c].Trim(), out int level))
                    break;

                if (level != expected)
                {
                    diagnostics.Add(new ContentDiagnostic(Cell(StagesTab, c, 1), $"Level 열은 1부터 차례로 둔다. 여기는 {expected}이어야 한다."));
                    return sheetRows;
                }

                lastLevelColumn = c;
            }

            for (int r = 1; r < rows.Count; r++)
            {
                string[] row = rows[r];
                int sheetRow = r + 1;

                if (IsBlank(row, lastLevelColumn))
                    continue;

                int before = diagnostics.Count;
                int stage = ReadInt(row, 0, StagesTab, sheetRow, diagnostics);
                int goal = ReadInt(row, 1, StagesTab, sheetRow, diagnostics);

                if (diagnostics.Count == before && stage != into.Count)
                    diagnostics.Add(new ContentDiagnostic(Cell(StagesTab, 0, sheetRow), $"성장도는 0부터 차례로 적는다. 여기는 {into.Count}이어야 한다."));

                var exps = new List<long>();
                int firstBlank = -1;

                for (int c = FirstLevelColumn; c <= lastLevelColumn; c++)
                {
                    if (Text(row, c).Length == 0)
                    {
                        if (firstBlank < 0)
                            firstBlank = c;

                        continue;
                    }

                    if (firstBlank >= 0)
                    {
                        diagnostics.Add(new ContentDiagnostic(Cell(StagesTab, c, sheetRow),
                            $"{Cell(StagesTab, firstBlank, sheetRow)}이 비어 있다. EXP는 왼쪽부터 빈 칸 없이 채운다."));
                        break;
                    }

                    exps.Add(ReadLong(row, c, StagesTab, sheetRow, diagnostics));
                }

                into.Add(new GrowthStageData { LevelExp = exps, GoalLevel = goal });
                sheetRows.Add(sheetRow);
            }

            return sheetRows;
        }

        private static List<int> ReadMilestones(List<string[]> rows, List<HqMilestoneData> into, List<ContentDiagnostic> diagnostics)
        {
            var sheetRows = new List<int>();

            if (!HasHeader(rows, MilestonesTab, new[] { "stage", "reward" }, diagnostics))
                return sheetRows;

            for (int r = 1; r < rows.Count; r++)
            {
                string[] row = rows[r];
                int sheetRow = r + 1;

                if (IsBlank(row, 1))
                    continue;

                into.Add(new HqMilestoneData
                {
                    Stage = ReadInt(row, 0, MilestonesTab, sheetRow, diagnostics),
                    Reward = ReadLong(row, 1, MilestonesTab, sheetRow, diagnostics),
                });
                sheetRows.Add(sheetRow);
            }

            return sheetRows;
        }

        // 로더의 경로("Growth.Stages[3].LevelExp")를 시트 위치로 바꾼다.
        private static string PlaceOf(string path, List<int> stageRows, List<int> milestoneRows, int lastLevelColumn)
        {
            if (TryIndex(path, "Growth.Stages[", out int stage, out string rest) && stage < stageRows.Count)
            {
                int row = stageRows[stage];

                return rest == ".GoalLevel"
                    ? Cell(StagesTab, 1, row)
                    : Range(StagesTab, FirstLevelColumn, Math.Max(FirstLevelColumn, lastLevelColumn), row);
            }

            if (TryIndex(path, "Growth.Milestones[", out int mark, out _) && mark < milestoneRows.Count)
                return Range(MilestonesTab, 0, 1, milestoneRows[mark]);

            return path.StartsWith("Growth.Milestones", StringComparison.Ordinal) ? MilestonesTab : StagesTab;
        }
    }
}
