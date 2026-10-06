using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using BlackHole.Core;

namespace BlackHole.Authoring
{
    // 데이터 시트 표를 읽는 공통 부품: 칸 글자, 머리칸 확인, 숫자 읽기, 시트 좌표("Growth!D12").
    // 읽지 못한 칸은 그 좌표로 진단을 더하고 0을 돌려준다. 진단이 하나라도 있으면 부르는 쪽이 결과를 쓰지 않는다.
    public static class SheetCells
    {
        public static string Text(string[] row, int column) => column < row.Length ? row[column].Trim() : string.Empty;

        public static bool IsBlank(string[] row, int lastColumn)
        {
            for (int c = 0; c <= lastColumn; c++)
            {
                if (Text(row, c).Length > 0)
                    return false;
            }

            return true;
        }

        public static bool HasHeader(List<string[]> rows, string tab, string[] titles, List<ContentDiagnostic> diagnostics)
        {
            if (rows.Count == 0)
            {
                diagnostics.Add(new ContentDiagnostic(tab, "비어 있다. 첫 행에 머리칸(" + string.Join(", ", titles) + ")이 필요하다."));
                return false;
            }

            bool ok = true;

            for (int c = 0; c < titles.Length; c++)
            {
                if (Text(rows[0], c) != titles[c])
                {
                    diagnostics.Add(new ContentDiagnostic(Cell(tab, c, 1), $"머리칸은 '{titles[c]}'이어야 한다."));
                    ok = false;
                }
            }

            return ok;
        }

        // key | value 표에서 키마다의 시트 행. 모르는 키, 두 번 나온 키, 빠진 키는 진단한다.
        // optionalKeys: 있어도 없어도 되는 키(없으면 호출자가 기존 값을 유지한다). 알려진 키로 치지만 빠졌다고 진단하지 않는다.
        public static Dictionary<string, int> KeyRows(List<string[]> rows, string tab, IReadOnlyCollection<string> keys, List<ContentDiagnostic> diagnostics,
            IReadOnlyCollection<string> optionalKeys = null)
        {
            var found = new Dictionary<string, int>(StringComparer.Ordinal);

            if (!HasHeader(rows, tab, new[] { "key", "value" }, diagnostics))
                return found;

            var known = new HashSet<string>(keys, StringComparer.Ordinal);
            string allowed = string.Join(", ", keys);

            if (optionalKeys != null)
            {
                known.UnionWith(optionalKeys);
                allowed += ", " + string.Join(", ", optionalKeys) + "(생략 가능)";
            }

            for (int r = 1; r < rows.Count; r++)
            {
                if (IsBlank(rows[r], 1))
                    continue;

                string key = Text(rows[r], 0);
                int sheetRow = r + 1;

                if (!known.Contains(key))
                    diagnostics.Add(new ContentDiagnostic(Cell(tab, 0, sheetRow), $"모르는 키다: '{key}'. 쓸 수 있는 키: {allowed}."));
                else if (found.TryGetValue(key, out int first))
                    diagnostics.Add(new ContentDiagnostic(Cell(tab, 0, sheetRow), $"'{key}'가 {first}행에도 있다."));
                else
                    found.Add(key, sheetRow);
            }

            foreach (string key in keys)
            {
                if (!found.ContainsKey(key))
                    diagnostics.Add(new ContentDiagnostic(tab, $"'{key}' 행이 없다."));
            }

            return found;
        }

        // 시트가 천 단위 쉼표를 붙여 내보내도("30,000") 읽는다.
        public static long ReadLong(string[] row, int column, string tab, int sheetRow, List<ContentDiagnostic> diagnostics)
        {
            string text = Text(row, column);

            if (long.TryParse(text, NumberStyles.Integer | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out long value))
                return value;

            diagnostics.Add(new ContentDiagnostic(Cell(tab, column, sheetRow),
                text.Length == 0 ? "비어 있다. 정수가 필요하다." : $"정수가 필요하다. 받은 값: '{text}'."));
            return 0;
        }

        public static int ReadInt(string[] row, int column, string tab, int sheetRow, List<ContentDiagnostic> diagnostics)
        {
            int before = diagnostics.Count;
            long value = ReadLong(row, column, tab, sheetRow, diagnostics);

            if (value >= int.MinValue && value <= int.MaxValue)
                return (int)value;

            if (diagnostics.Count == before)
                diagnostics.Add(new ContentDiagnostic(Cell(tab, column, sheetRow), "값이 너무 크다."));

            return 0;
        }

        // 소수는 점(.)으로 적는다. 칸 서식이 %이면("5%") 100으로 나눈다.
        public static float ReadFloat(string[] row, int column, string tab, int sheetRow, List<ContentDiagnostic> diagnostics)
        {
            string text = Text(row, column);
            bool percent = text.EndsWith("%", StringComparison.Ordinal);
            string number = percent ? text.Substring(0, text.Length - 1).Trim() : text;

            if (float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                return percent ? value / 100 : value;

            diagnostics.Add(new ContentDiagnostic(Cell(tab, column, sheetRow),
                text.Length == 0 ? "비어 있다. 숫자가 필요하다." : $"숫자가 필요하다(소수는 점으로). 받은 값: '{text}'."));
            return 0;
        }

        // 로더의 경로에서 번호를 읽는다: TryIndex("Growth.Milestones[3].Level", "Growth.Milestones[") → 3, ".Level".
        public static bool TryIndex(string path, string prefix, out int index, out string rest)
        {
            index = -1;
            rest = string.Empty;

            if (!path.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            int end = path.IndexOf(']', prefix.Length);

            if (end < 0 || !int.TryParse(path.Substring(prefix.Length, end - prefix.Length), out index))
                return false;

            rest = path.Substring(end + 1);
            return true;
        }

        public static string Cell(string tab, int column, int sheetRow) => $"{tab}!{Column(column)}{sheetRow}";

        public static string Range(string tab, int firstColumn, int lastColumn, int sheetRow) =>
            $"{Cell(tab, firstColumn, sheetRow)}:{Column(lastColumn)}{sheetRow}";

        // 0 → A, 25 → Z, 26 → AA.
        public static string Column(int index)
        {
            string name = string.Empty;

            for (int n = index + 1; n > 0; n = (n - 1) / 26)
                name = (char)('A' + (n - 1) % 26) + name;

            return name;
        }

        public static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

        // 다시 읽으면 같은 float이 되는 가장 짧은 글자.
        public static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);

        // 정의 생성자의 예외 문장에 런타임이 덧붙인 매개변수 이름. 없으면 null. 어느 칸의 규칙인지 찾을 때 쓴다.
        public static string ParameterOf(string message)
        {
            Match match = Regex.Match(message, @"Parameter name: (\w+)|\(Parameter '(\w+)'\)");

            if (!match.Success)
                return null;

            return match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
        }

        // 정의 생성자의 예외 문장에서 런타임이 덧붙인 매개변수 이름을 뗀다(로더가 넘긴 진단의 문장도 같다). 위치는 시트 좌표가 알려 준다.
        public static string RuleMessage(ArgumentException error) => RuleMessage(error.Message);

        public static string RuleMessage(string message)
        {
            int cut = message.IndexOf("\nParameter name:", StringComparison.Ordinal);

            if (cut < 0)
                cut = message.IndexOf(" (Parameter '", StringComparison.Ordinal);

            return cut >= 0 ? message.Substring(0, cut).TrimEnd() : message;
        }
    }
}
