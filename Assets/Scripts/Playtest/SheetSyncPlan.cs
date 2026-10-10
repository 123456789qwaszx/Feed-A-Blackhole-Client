#if UNITY_EDITOR || SHEET_SYNC_TEST
using System;
using System.Collections.Generic;
using System.Globalization;

namespace BlackHole.Unity
{
    // 기획 시트에 쓸 칸 하나: 노드 비용(NodeCost.Cost) 또는 효과(NodeEffects.Value).
    // Before는 시트에 있어야 할 지금 값(처음 변경의 이전 값), After는 쓸 값(마지막 변경의 이후 값).
    internal sealed class SheetUpdate
    {
        public string Path;
        public string Tab;
        public string NodeId;
        public int Rank;
        public string StatId;
        public string Column;
        public double Before;
        public double After;
        public readonly List<string> RecordIds = new List<string>();

        public string Describe() => $"{Tab} {NodeId} Rank {Rank}{(StatId != null ? " " + StatId : string.Empty)} · {Column}";

        // 웹 앱 write 요청의 한 칸.
        public JsonObject ToRequest()
        {
            var keys = new JsonObject { { "NodeId", NodeId }, { "Rank", Rank.ToString(CultureInfo.InvariantCulture) } };
            if (StatId != null)
                keys.Add("StatId", StatId);

            return new JsonObject
            {
                { "tab", Tab },
                { "keys", keys },
                { "column", Column },
                { "value", After },
                { "expected", Before },
            };
        }
    }

    // 시트의 그 칸이 지금 어떤가.
    internal enum SheetCellState
    {
        // 시트가 아직 이전 값이다(이 변경을 시트에 써야 한다). 이대로 끌어오면 레포의 변경이 사라진다.
        Missing,
        // 시트가 이미 새 값이다(누가 옮겼다).
        Present,
        // 시트가 이전 값도 새 값도 아니다(시트에서 따로 바뀌었다).
        Conflict,
        // 시트에서 그 행을 찾지 못했다.
        NotFound,
    }

    // 승격·되돌리기(M4)로 바뀐 노드 값 가운데 아직 시트에 없는 것을 모아 시트에 쓸 칸을 만든다(M5).
    internal static class SheetSyncPlan
    {
        // 시트에 옮길 기록: 노드 경로가 있는 promote·revert 가운데 아직 시트와 맞춰지지 않은 것(기록 순서).
        public static List<ChangeRecord> Pending(IReadOnlyList<ChangeRecord> records)
        {
            HashSet<string> synced = ChangeRecord.SyncedIds(records);
            var pending = new List<ChangeRecord>();

            foreach (ChangeRecord record in records)
            {
                if ((record.Kind == ChangeRecord.Promote || record.Kind == ChangeRecord.Revert)
                    && !synced.Contains(record.Id)
                    && record.Patches.Exists(patch => IsNodePath(patch.Path)))
                    pending.Add(record);
            }

            return pending;
        }

        // 칸마다 하나로 합친다: 처음 이전 값 → 마지막 이후 값. 바꿨다 되돌려 같아진 칸은 빠진다(쓸 것이 없다).
        public static List<SheetUpdate> NetUpdates(IReadOnlyList<ChangeRecord> pending)
        {
            var byCell = new Dictionary<string, SheetUpdate>(StringComparer.Ordinal);
            var order = new List<string>();

            foreach (ChangeRecord record in pending)
            {
                foreach (ChangePatch patch in record.Patches)
                {
                    if (!IsNodePath(patch.Path) || !ProfileTargets.TryMap(patch.Path, out ProfileTarget target, out _))
                        continue;

                    if (!byCell.TryGetValue(patch.Path, out SheetUpdate update))
                    {
                        update = new SheetUpdate
                        {
                            Path = patch.Path,
                            Tab = target.Source == TargetSource.NodeCostCsv ? SheetTabs.NodeCost : SheetTabs.NodeEffects,
                            NodeId = target.NodeId,
                            Rank = target.Rank,
                            StatId = target.StatId,
                            Column = target.Column,
                            Before = patch.Before,
                        };
                        byCell.Add(patch.Path, update);
                        order.Add(patch.Path);
                    }

                    update.After = patch.After;

                    if (!update.RecordIds.Contains(record.Id))
                        update.RecordIds.Add(record.Id);
                }
            }

            var updates = new List<SheetUpdate>();
            foreach (string path in order)
            {
                SheetUpdate update = byCell[path];
                if (!Same(update.Before, update.After))
                    updates.Add(update);
            }

            return updates;
        }

        // 시트에서 읽은 CSV로 그 칸의 상태를 본다.
        public static SheetCellState StateOf(SheetUpdate update, string sheetCsv, out double sheetValue)
        {
            sheetValue = 0;
            bool found = update.Tab == SheetTabs.NodeCost
                ? NodeSheetEdits.TryGetCost(sheetCsv, update.NodeId, update.Rank, out long cost, out _) && Set(out sheetValue, cost)
                : NodeSheetEdits.TryGetEffect(sheetCsv, update.NodeId, update.Rank, update.StatId, out sheetValue, out _);

            if (!found)
                return SheetCellState.NotFound;

            if (Same(sheetValue, update.After))
                return SheetCellState.Present;

            return Same(sheetValue, update.Before) ? SheetCellState.Missing : SheetCellState.Conflict;
        }

        public static bool IsNodePath(string path) => path != null && path.StartsWith("node/", StringComparison.Ordinal);

        public static bool Same(double a, double b) => Math.Abs(a - b) <= 1e-6 * Math.Max(1, Math.Max(Math.Abs(a), Math.Abs(b)));

        private static bool Set(out double into, double value)
        {
            into = value;
            return true;
        }
    }
}
#endif
