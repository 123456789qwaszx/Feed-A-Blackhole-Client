#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BlackHole.Unity
{
    // 노드 CSV(NodeCost·NodeEffects)의 칸 하나를 고친다(M4 승격·되돌리기). 시트에서 내려받은 모양을 그대로 지킨다:
    // - 고친 칸 말고는 한 글자도 바꾸지 않는다(줄 순서·줄 끝·다른 칸의 따옴표·분석용 칸).
    // - 비용은 시트처럼 천 단위 쉼표로 적고, 쉼표가 들어가면 따옴표로 감싼다("545,000"). 효과 값은 쉼표 없이 소수점만.
    // - NodeEffects의 표시 칸("+25%")은 지금 글자가 이전 값의 표시와 같을 때만 새 값에 맞춘다(손으로 적은 글은 두지 않는다).
    internal static class NodeSheetEdits
    {
        public const string CostColumn = "Cost";
        public const string ValueColumn = "Value";
        public const string UnitColumn = "단위";
        public const string DisplayColumn = "표시";

        // NodeCost.csv에서 (노드, Rank)의 비용을 value로. before는 이전 값.
        public static bool TrySetCost(string csv, string nodeId, int rank, long value, out string result, out long before, out string error)
        {
            result = null;
            before = 0;

            if (!Table.TryOpen(csv, out Table table, out error))
                return false;

            if (!table.TryFind(new[] { ("NodeId", nodeId), ("Rank", Int(rank)) }, out int line, out error))
                return false;

            string cell = table.Cell(line, CostColumn, out error);

            if (cell == null)
                return false;

            if (!long.TryParse(cell, NumberStyles.Integer | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out before))
            {
                error = $"비용 칸이 정수가 아니다: '{cell}'.";
                return false;
            }

            table.Set(line, CostColumn, FormatCost(value));
            result = table.Text();
            return true;
        }

        // NodeEffects.csv에서 (노드, Rank, StatId)의 효과 값을 value로. 표시 칸도 맞춘다(위 규칙).
        public static bool TrySetEffect(string csv, string nodeId, int rank, string statId, double value, out string result, out double before, out string error)
        {
            result = null;
            before = 0;

            if (!Table.TryOpen(csv, out Table table, out error))
                return false;

            if (!table.TryFind(new[] { ("NodeId", nodeId), ("Rank", Int(rank)), ("StatId", statId) }, out int line, out error))
                return false;

            string cell = table.Cell(line, ValueColumn, out error);

            if (cell == null)
                return false;

            if (!double.TryParse(cell, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out before))
            {
                error = $"효과 값 칸이 숫자가 아니다: '{cell}'.";
                return false;
            }

            table.Set(line, ValueColumn, FormatReal(value));

            if (table.Has(DisplayColumn) && table.Has(UnitColumn))
            {
                string unit = table.Cell(line, UnitColumn, out _);
                string display = table.Cell(line, DisplayColumn, out _);

                if (display == DisplayOf(before, unit))
                    table.Set(line, DisplayColumn, DisplayOf(value, unit));
            }

            result = table.Text();
            return true;
        }

        // 읽기만: 지금 비용·효과 값.
        public static bool TryGetCost(string csv, string nodeId, int rank, out long value, out string error)
        {
            value = 0;

            if (!Table.TryOpen(csv, out Table table, out error) || !table.TryFind(new[] { ("NodeId", nodeId), ("Rank", Int(rank)) }, out int line, out error))
                return false;

            string cell = table.Cell(line, CostColumn, out error);
            return cell != null && long.TryParse(cell, NumberStyles.Integer | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value);
        }

        public static bool TryGetEffect(string csv, string nodeId, int rank, string statId, out double value, out string error)
        {
            value = 0;

            if (!Table.TryOpen(csv, out Table table, out error) ||
                !table.TryFind(new[] { ("NodeId", nodeId), ("Rank", Int(rank)), ("StatId", statId) }, out int line, out error))
                return false;

            string cell = table.Cell(line, ValueColumn, out error);
            return cell != null && double.TryParse(cell, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value);
        }

        // 시트의 비용 서식: 천 단위 쉼표(350, 545,000).
        public static string FormatCost(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

        // 효과 값 서식: 쉼표 없는 짧은 십진수(25, 12.5, 0.35).
        public static string FormatReal(double value) =>
            Math.Abs(value) < 1e15
                ? ((decimal)value).ToString("0.############################", CultureInfo.InvariantCulture)
                : value.ToString("R", CultureInfo.InvariantCulture);

        // 표시 칸 규칙(시트와 같다): 부호 + 값 + (Percent면 %). 예: +25%, +2, -10%.
        public static string DisplayOf(double value, string unit) =>
            (value < 0 ? string.Empty : "+") + FormatReal(value) + (unit == "Percent" ? "%" : string.Empty);

        private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

        // 줄과 칸의 원래 글자를 그대로 쥔 CSV. 한 칸이 여러 줄인 CSV(따옴표 안 줄바꿈)는 다루지 않는다.
        private sealed class Table
        {
            private readonly List<string> _lines = new List<string>();
            private readonly List<string> _endings = new List<string>();
            private readonly Dictionary<string, int> _columns = new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly Dictionary<int, List<string>> _changed = new Dictionary<int, List<string>>();
            private string _bom = string.Empty;

            public static bool TryOpen(string csv, out Table table, out string error)
            {
                table = new Table();
                error = null;

                if (string.IsNullOrEmpty(csv))
                {
                    error = "CSV가 비어 있다.";
                    return false;
                }

                string text = csv;
                if (text[0] == '﻿')
                {
                    table._bom = "﻿";
                    text = text.Substring(1);
                }

                int start = 0;
                while (start <= text.Length)
                {
                    int end = text.IndexOf('\n', start);

                    if (end < 0)
                    {
                        if (start < text.Length)
                        {
                            table._lines.Add(text.Substring(start));
                            table._endings.Add(string.Empty);
                        }

                        break;
                    }

                    bool crlf = end > start && text[end - 1] == '\r';
                    table._lines.Add(text.Substring(start, (crlf ? end - 1 : end) - start));
                    table._endings.Add(crlf ? "\r\n" : "\n");
                    start = end + 1;
                }

                for (int i = 0; i < table._lines.Count; i++)
                {
                    if (CountQuotes(table._lines[i]) % 2 != 0)
                    {
                        error = $"{i + 1}행: 따옴표가 짝이 맞지 않는다(한 칸이 여러 줄인 CSV는 고치지 않는다).";
                        return false;
                    }
                }

                if (table._lines.Count == 0)
                {
                    error = "머리칸이 없다.";
                    return false;
                }

                List<string> header = Split(table._lines[0]);
                for (int c = 0; c < header.Count; c++)
                {
                    string name = Unquote(header[c]).Trim();
                    if (name.Length > 0 && !table._columns.ContainsKey(name))
                        table._columns.Add(name, c);
                }

                return true;
            }

            public bool Has(string column) => _columns.ContainsKey(column);

            public bool TryFind(IReadOnlyList<(string Column, string Value)> keys, out int found, out string error)
            {
                found = -1;

                foreach ((string column, string _) in keys)
                {
                    if (!Has(column))
                    {
                        error = $"머리칸 '{column}'이 없다.";
                        return false;
                    }
                }

                for (int line = 1; line < _lines.Count; line++)
                {
                    List<string> cells = Split(_lines[line]);
                    bool match = true;

                    foreach ((string column, string value) in keys)
                    {
                        int c = _columns[column];
                        if (c >= cells.Count || Unquote(cells[c]).Trim() != value)
                        {
                            match = false;
                            break;
                        }
                    }

                    if (!match)
                        continue;

                    if (found >= 0)
                    {
                        error = $"같은 행이 여러 개다({found + 1}행, {line + 1}행): {Describe(keys)}.";
                        return false;
                    }

                    found = line;
                }

                error = found < 0 ? $"행이 없다: {Describe(keys)}." : null;
                return found >= 0;
            }

            public string Cell(int line, string column, out string error)
            {
                if (!_columns.TryGetValue(column, out int c))
                {
                    error = $"머리칸 '{column}'이 없다.";
                    return null;
                }

                List<string> cells = Cells(line);

                if (c >= cells.Count)
                {
                    error = $"{line + 1}행에 '{column}' 칸이 없다.";
                    return null;
                }

                error = null;
                return Unquote(cells[c]).Trim();
            }

            public void Set(int line, string column, string value)
            {
                List<string> cells = Cells(line);
                int c = _columns[column];

                while (cells.Count <= c)
                    cells.Add(string.Empty);

                cells[c] = Quote(value);
                _changed[line] = cells;
            }

            public string Text()
            {
                var text = new StringBuilder(_bom);

                for (int i = 0; i < _lines.Count; i++)
                {
                    text.Append(_changed.TryGetValue(i, out List<string> cells) ? string.Join(",", cells) : _lines[i]);
                    text.Append(_endings[i]);
                }

                return text.ToString();
            }

            private List<string> Cells(int line) => _changed.TryGetValue(line, out List<string> cells) ? cells : Split(_lines[line]);

            // 한 줄을 칸의 원래 글자(따옴표 포함)로 나눈다.
            private static List<string> Split(string line)
            {
                var cells = new List<string>();
                int start = 0;
                bool quoted = false;

                for (int i = 0; i < line.Length; i++)
                {
                    char c = line[i];

                    if (c == '"')
                        quoted = !quoted;
                    else if (c == ',' && !quoted)
                    {
                        cells.Add(line.Substring(start, i - start));
                        start = i + 1;
                    }
                }

                cells.Add(line.Substring(start));
                return cells;
            }

            private static string Unquote(string raw)
            {
                string cell = raw.Trim();
                return cell.Length >= 2 && cell[0] == '"' && cell[cell.Length - 1] == '"'
                    ? cell.Substring(1, cell.Length - 2).Replace("\"\"", "\"")
                    : raw;
            }

            private static string Quote(string value) =>
                value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;

            private static int CountQuotes(string line)
            {
                int count = 0;
                foreach (char c in line)
                {
                    if (c == '"')
                        count++;
                }

                return count;
            }

            private static string Describe(IReadOnlyList<(string Column, string Value)> keys)
            {
                var parts = new List<string>();
                foreach ((string column, string value) in keys)
                    parts.Add($"{column}={value}");

                return string.Join(", ", parts);
            }
        }
    }
}
#endif
