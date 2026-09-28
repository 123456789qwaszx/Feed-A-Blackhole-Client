using System;
using System.Collections.Generic;
using BlackHole.Core;
using static BlackHole.Authoring.SheetCells;

namespace BlackHole.Authoring
{
    // 노드의 수치(가격·업그레이드)와 데이터 시트의 탭 사이의 변환. 노드의 칸·선·시작 노드는 노드 도구가 맡고 시트는 건드리지 않는다.
    // - Nodes 탭: id | price. 노드 도구의 노드를 모두, 그 노드만 적는다(노드를 더하고 빼는 것은 노드 도구에서 한다).
    // - NodeUpgrades 탭: node | stat | operation | value | meaning. 한 행이 업그레이드 하나이고, 같은 노드의 행은 위에서부터 차례다.
    //   stat은 UpgradeStats 탭의 이름, operation은 Add·Percent·Multiply. meaning은 값의 뜻을 보이는 수식이라 읽지 않는다.
    // - UpgradeStats 탭: 쓸 수 있는 수치 이름(내보내기만 한다). NodeUpgrades의 stat 열 드롭다운의 원본으로 쓴다.
    // 규칙은 게임과 같은 로더(NodeTreeLoader)로 보고, 노드 도구의 검사 가운데 값을 줄이는 곱하기는 경고로 알린다.
    public static class NodeSheet
    {
        public const string NodesTab = "Nodes";
        public const string UpgradesTab = "NodeUpgrades";
        public const string StatsTab = "UpgradeStats";

        private static readonly string[] _upgradeColumns = { "node", "stat", "operation", "value" };

        public static string NodesCsv(NodeTreeData tree)
        {
            var rows = new List<IReadOnlyList<string>> { new[] { "id", "price" } };

            foreach (NodeData node in tree.Nodes)
                rows.Add(new[] { node.Id, Number(node.Price) });

            return Csv.Write(rows);
        }

        public static string UpgradesCsv(NodeTreeData tree)
        {
            var rows = new List<IReadOnlyList<string>> { new[] { "node", "stat", "operation", "value", "meaning" } };

            foreach (NodeData node in tree.Nodes)
            {
                foreach (UpgradeData upgrade in node.Upgrades ?? new List<UpgradeData>())
                    rows.Add(new[] { node.Id, upgrade.Stat, upgrade.Operation.ToString(), Number(upgrade.Value), Meaning(rows.Count + 1) });
            }

            return Csv.Write(rows);
        }

        public static string StatsCsv(IReadOnlyList<(string Name, string Note)> stats)
        {
            var rows = new List<IReadOnlyList<string>> { new[] { "stat", "note" } };

            foreach ((string name, string note) in stats)
                rows.Add(new[] { name, note });

            return Csv.Write(rows);
        }

        // 노드 도구의 트리를 바꾸지 않고 수치만 채울 복사본.
        public static NodeTreeData Copy(NodeTreeData tree)
        {
            var copy = new NodeTreeData();

            foreach (NodeData node in tree.Nodes)
            {
                var upgrades = new List<UpgradeData>();

                foreach (UpgradeData upgrade in node.Upgrades ?? new List<UpgradeData>())
                    upgrades.Add(new UpgradeData { Stat = upgrade.Stat, Operation = upgrade.Operation, Value = upgrade.Value });

                copy.Nodes.Add(new NodeData
                {
                    Id = node.Id, Price = node.Price, Start = node.Start, X = node.X, Y = node.Y,
                    Links = new List<string>(node.Links ?? new List<string>()), Upgrades = upgrades,
                });
            }

            return copy;
        }

        // 두 탭을 읽어 tree(복사본)의 가격·업그레이드를 바꾼다. 반환: 진단(없으면 통과). 값을 줄이는 곱하기는 warnings에 더한다.
        public static List<ContentDiagnostic> Read(string nodesCsv, string upgradesCsv, NodeTreeData tree, IReadOnlyCollection<string> stats,
            List<ContentDiagnostic> warnings)
        {
            var diagnostics = new List<ContentDiagnostic>();
            var byId = new Dictionary<string, NodeData>(StringComparer.Ordinal);

            foreach (NodeData node in tree.Nodes)
                byId[node.Id] = node;

            Dictionary<string, int> priceRows = ReadNodes(Csv.Parse(nodesCsv ?? string.Empty), byId, diagnostics);
            var upgradeRows = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            var upgrades = ReadUpgrades(Csv.Parse(upgradesCsv ?? string.Empty), priceRows, stats, upgradeRows, diagnostics);

            if (diagnostics.Count > 0)
                return diagnostics;

            foreach (NodeData node in tree.Nodes)
                node.Upgrades = upgrades.TryGetValue(node.Id, out List<UpgradeData> list) ? list : new List<UpgradeData>();

            NodeTreeLoadResult load = NodeTreeLoader.Load(tree);

            foreach (ContentDiagnostic rule in load.Diagnostics)
                diagnostics.Add(new ContentDiagnostic(PlaceOf(rule, priceRows, upgradeRows), RuleMessage(rule.Message)));

            foreach (ContentDiagnostic check in NodeTreeAuthoring.Check(tree))
            {
                if (check.Path.Contains(".Upgrades["))
                    warnings.Add(new ContentDiagnostic(PlaceOf(check, priceRows, upgradeRows), check.Message));
            }

            return diagnostics;
        }

        // 반환: 노드 ID마다 시트 행. 읽은 가격은 byId의 노드에 바로 쓴다(tree는 복사본이다).
        private static Dictionary<string, int> ReadNodes(List<string[]> table, Dictionary<string, NodeData> byId, List<ContentDiagnostic> diagnostics)
        {
            var rows = new Dictionary<string, int>(StringComparer.Ordinal);

            if (!HasHeader(table, NodesTab, new[] { "id", "price" }, diagnostics))
                return rows;

            for (int r = 1; r < table.Count; r++)
            {
                string[] row = table[r];
                int sheetRow = r + 1;

                if (IsBlank(row, 1))
                    continue;

                string id = Text(row, 0);

                if (!byId.TryGetValue(id, out NodeData node))
                    diagnostics.Add(new ContentDiagnostic(Cell(NodesTab, 0, sheetRow),
                        $"노드 도구에 없는 ID다: '{id}'. 노드를 더하려면 먼저 노드 도구(BlackHole > Node Tree)에서 놓는다."));
                else if (rows.TryGetValue(id, out int first))
                    diagnostics.Add(new ContentDiagnostic(Cell(NodesTab, 0, sheetRow), $"'{id}'가 {first}행에도 있다."));
                else
                {
                    node.Price = ReadLong(row, 1, NodesTab, sheetRow, diagnostics);
                    rows.Add(id, sheetRow);
                }
            }

            foreach (string id in byId.Keys)
            {
                if (!rows.ContainsKey(id))
                    diagnostics.Add(new ContentDiagnostic(NodesTab, $"'{id}' 행이 없다. 노드 도구의 노드는 모두 적는다."));
            }

            return rows;
        }

        private static Dictionary<string, List<UpgradeData>> ReadUpgrades(List<string[]> table, Dictionary<string, int> nodes,
            IReadOnlyCollection<string> stats, Dictionary<string, List<int>> rows, List<ContentDiagnostic> diagnostics)
        {
            var upgrades = new Dictionary<string, List<UpgradeData>>(StringComparer.Ordinal);
            var known = new HashSet<string>(stats, StringComparer.Ordinal);

            if (!HasHeader(table, UpgradesTab, _upgradeColumns, diagnostics))
                return upgrades;

            for (int r = 1; r < table.Count; r++)
            {
                string[] row = table[r];
                int sheetRow = r + 1;

                if (IsBlank(row, _upgradeColumns.Length - 1))
                    continue;

                string id = Text(row, 0);
                string stat = Text(row, 1);
                string operation = Text(row, 2);

                if (!nodes.ContainsKey(id))
                    diagnostics.Add(new ContentDiagnostic(Cell(UpgradesTab, 0, sheetRow), $"Nodes 탭에 없는 노드다: '{id}'."));

                if (!known.Contains(stat))
                    diagnostics.Add(new ContentDiagnostic(Cell(UpgradesTab, 1, sheetRow), $"모르는 수치 이름이다: '{stat}'. UpgradeStats 탭의 이름을 쓴다."));

                if (!Enum.TryParse(operation, false, out UpgradeOperation parsed) || !Enum.IsDefined(typeof(UpgradeOperation), parsed)
                    || int.TryParse(operation, out _))
                    diagnostics.Add(new ContentDiagnostic(Cell(UpgradesTab, 2, sheetRow), $"연산은 Add, Percent, Multiply 가운데 하나다. 받은 값: '{operation}'."));

                float value = ReadFloat(row, 3, UpgradesTab, sheetRow, diagnostics);

                if (!upgrades.TryGetValue(id, out List<UpgradeData> list))
                {
                    upgrades.Add(id, list = new List<UpgradeData>());
                    rows.Add(id, new List<int>());
                }

                list.Add(new UpgradeData { Stat = stat, Operation = parsed, Value = value });
                rows[id].Add(sheetRow);
            }

            return upgrades;
        }

        // 로더·도구의 경로("Nodes[a].Upgrades[1]")와 문장의 매개변수 이름으로 시트 위치를 찾는다.
        // 선·시작 노드처럼 노드 도구가 맡는 칸의 오류는 노드 도구에서 고친다.
        private static string PlaceOf(ContentDiagnostic rule, Dictionary<string, int> priceRows, Dictionary<string, List<int>> upgradeRows)
        {
            string path = rule.Path;

            if (!path.StartsWith("Nodes[", StringComparison.Ordinal))
                return "노드 도구 " + path;

            int end = path.IndexOf(']');
            string id = path.Substring(6, end - 6);
            string rest = path.Substring(end + 1);

            if (TryIndex(rest, ".Upgrades[", out int index, out _) && upgradeRows.TryGetValue(id, out List<int> rows) && index < rows.Count)
            {
                int column = Array.IndexOf(_upgradeColumns, ParameterOf(rule.Message) ?? string.Empty);
                return column > 0 ? Cell(UpgradesTab, column, rows[index]) : Range(UpgradesTab, 0, _upgradeColumns.Length - 1, rows[index]);
            }

            // 노드 하나의 규칙(NodeDefinition 생성자) 가운데 가격만 시트의 것이다. 닿지 않는 노드 같은 오류도 같은 경로로 온다.
            if (rest.Length == 0 && ParameterOf(rule.Message) == "price" && priceRows.TryGetValue(id, out int row))
                return Cell(NodesTab, 1, row);

            return "노드 도구 " + path;
        }

        // 값의 뜻을 보이는 시트 수식: 더하기 +1, 비율 +25%, 곱하기 ×10.
        private static string Meaning(int sheetRow) =>
            $"=IFS(C{sheetRow}=\"Add\",IF(D{sheetRow}>=0,\"+\",\"\")&D{sheetRow}," +
            $"C{sheetRow}=\"Percent\",IF(D{sheetRow}>=0,\"+\",\"\")&D{sheetRow}*100&\"%\"," +
            $"C{sheetRow}=\"Multiply\",\"×\"&D{sheetRow},TRUE,\"?\")";
    }
}
