using System;
using System.Collections.Generic;
using BlackHole.Core;
using static BlackHole.Authoring.SheetCells;

namespace BlackHole.Authoring
{
    // 적 공급 설정(ContentData.Enemies의 출현 배치·전체 개체 수 상한·전투 시작 공급)과 데이터 시트의 두 탭 사이의 변환.
    // - Supply 탭: key | value | note. 키는 min-distance, max-distance, max-alive-enemies이고 모두 있어야 한다.
    //   pickup-inner-offset, pickup-outer-offset(픽업 출현 띠의 오프셋, 일반 띠 바깥 반지름 기준)은 생략할 수 있다 — 없으면 지금 값을 유지한다.
    // - StartSupply 탭: kind | count. 한 행이 공급 한 건이고, 위에서부터 요청 순서다. kind는 적 종류의 ID다.
    // 규칙은 게임과 같은 로더(EnemyContentLoader)로 본다. 적 종류는 into에 이미 있는 것(적 종류 목록 에셋)을 쓴다.
    public static class SupplySheet
    {
        public const string SupplyTab = "Supply";
        public const string StartSupplyTab = "StartSupply";

        private const string MinDistance = "min-distance";
        private const string MaxDistance = "max-distance";
        private const string MaxAliveEnemies = "max-alive-enemies";
        private const string PickupInnerOffset = "pickup-inner-offset";
        private const string PickupOuterOffset = "pickup-outer-offset";

        private static readonly string[] _keys = { MinDistance, MaxDistance, MaxAliveEnemies };
        private static readonly string[] _optionalKeys = { PickupInnerOffset, PickupOuterOffset };

        public static string SupplyCsv(EnemyContentData data)
        {
            EnemyPlacementData placement = data.EnemyPlacement ?? new EnemyPlacementData();
            PickupPlacementData pickup = data.PickupPlacement ?? new PickupPlacementData();

            return Csv.Write(new List<IReadOnlyList<string>>
            {
                new[] { "key", "value", "note" },
                new[] { MinDistance, Number(placement.MinDistance), "적이 나오는 띠의 안쪽 반지름(HQ로부터)." },
                new[] { MaxDistance, Number(placement.MaxDistance), "적이 나오는 띠의 바깥 반지름." },
                new[] { MaxAliveEnemies, Number(data.MaxAliveEnemies), "한 판에 동시에 살아 있을 수 있는 적의 최대 수(성능 예산). 넘는 생성 요청은 버린다." },
                new[] { PickupInnerOffset, Number(pickup.InnerOffset), "픽업(혜성)이 나오는 띠의 안쪽 = 일반 띠 바깥 반지름 + 이 값(음수는 안쪽). 일반 띠가 바뀌어도 따라간다." },
                new[] { PickupOuterOffset, Number(pickup.OuterOffset), "픽업이 나오는 띠의 바깥 = 일반 띠 바깥 반지름 + 이 값. 안쪽 오프셋 이상이어야 한다." },
            });
        }

        public static string StartSupplyCsv(EnemyContentData data)
        {
            var rows = new List<IReadOnlyList<string>> { new[] { "kind", "count" } };

            foreach (SupplyData supply in data.StartSupply)
                rows.Add(new[] { supply.Enemy ?? string.Empty, Number(supply.Count) });

            return Csv.Write(rows);
        }

        // 두 탭을 읽어 통과하면 into.Enemies의 출현 배치·상한·전투 시작 공급을 바꾼다. 반환: 진단(없으면 통과).
        public static List<ContentDiagnostic> Read(string supplyCsv, string startSupplyCsv, ContentData into)
        {
            var diagnostics = new List<ContentDiagnostic>();
            List<string[]> supplyRows = Csv.Parse(supplyCsv ?? string.Empty);
            Dictionary<string, int> keyRows = KeyRows(supplyRows, SupplyTab, _keys, diagnostics, _optionalKeys);
            var sheet = new EnemyContentData { Enemies = into.Enemies.Enemies };

            if (diagnostics.Count == 0)
            {
                sheet.EnemyPlacement = new EnemyPlacementData
                {
                    MinDistance = ReadFloat(supplyRows[keyRows[MinDistance] - 1], 1, SupplyTab, keyRows[MinDistance], diagnostics),
                    MaxDistance = ReadFloat(supplyRows[keyRows[MaxDistance] - 1], 1, SupplyTab, keyRows[MaxDistance], diagnostics),
                };
                sheet.MaxAliveEnemies = ReadInt(supplyRows[keyRows[MaxAliveEnemies] - 1], 1, SupplyTab, keyRows[MaxAliveEnemies], diagnostics);

                // 픽업 띠는 생략할 수 있다: 키가 없으면 지금 값을 그대로 둔다. 둘 중 하나만 있어도 나머지는 지금 값이다.
                PickupPlacementData current = into.Enemies.PickupPlacement ?? new PickupPlacementData();
                sheet.PickupPlacement = new PickupPlacementData
                {
                    InnerOffset = keyRows.TryGetValue(PickupInnerOffset, out int inner)
                        ? ReadFloat(supplyRows[inner - 1], 1, SupplyTab, inner, diagnostics) : current.InnerOffset,
                    OuterOffset = keyRows.TryGetValue(PickupOuterOffset, out int outer)
                        ? ReadFloat(supplyRows[outer - 1], 1, SupplyTab, outer, diagnostics) : current.OuterOffset,
                };
            }

            List<int> startRows = ReadStartSupply(Csv.Parse(startSupplyCsv ?? string.Empty), sheet.StartSupply, diagnostics);

            if (diagnostics.Count > 0)
                return diagnostics;

            var rules = new List<ContentDiagnostic>();
            EnemyContentLoader.Load(sheet, rules);

            foreach (ContentDiagnostic rule in rules)
                diagnostics.Add(new ContentDiagnostic(PlaceOf(rule.Path, keyRows, startRows), RuleMessage(rule.Message)));

            if (diagnostics.Count == 0)
            {
                into.Enemies.EnemyPlacement = sheet.EnemyPlacement;
                into.Enemies.PickupPlacement = sheet.PickupPlacement;
                into.Enemies.MaxAliveEnemies = sheet.MaxAliveEnemies;
                into.Enemies.StartSupply = sheet.StartSupply;
            }

            return diagnostics;
        }

        // 반환: 공급마다의 시트 행 번호.
        private static List<int> ReadStartSupply(List<string[]> rows, List<SupplyData> into, List<ContentDiagnostic> diagnostics)
        {
            var sheetRows = new List<int>();

            if (!HasHeader(rows, StartSupplyTab, new[] { "kind", "count" }, diagnostics))
                return sheetRows;

            for (int r = 1; r < rows.Count; r++)
            {
                string[] row = rows[r];
                int sheetRow = r + 1;

                if (IsBlank(row, 1))
                    continue;

                into.Add(new SupplyData { Enemy = Text(row, 0), Count = ReadInt(row, 1, StartSupplyTab, sheetRow, diagnostics) });
                sheetRows.Add(sheetRow);
            }

            return sheetRows;
        }

        // 로더의 경로("StartSupply[0].Enemy", "MaxAliveEnemies")를 시트 위치로 바꾼다.
        private static string PlaceOf(string path, Dictionary<string, int> keyRows, List<int> startRows)
        {
            if (TryIndex(path, "StartSupply[", out int index, out string rest) && index < startRows.Count)
                return rest == ".Enemy" ? Cell(StartSupplyTab, 0, startRows[index]) : Range(StartSupplyTab, 0, 1, startRows[index]);

            if (path == "MaxAliveEnemies")
                return Cell(SupplyTab, 1, keyRows[MaxAliveEnemies]);

            if (path == "EnemyPlacement")
                return $"{Cell(SupplyTab, 1, keyRows[MinDistance])}, {Cell(SupplyTab, 1, keyRows[MaxDistance])}";

            if (path == "PickupPlacement")
                return keyRows.ContainsKey(PickupInnerOffset) && keyRows.ContainsKey(PickupOuterOffset)
                    ? $"{Cell(SupplyTab, 1, keyRows[PickupInnerOffset])}, {Cell(SupplyTab, 1, keyRows[PickupOuterOffset])}"
                    : "Supply 탭의 픽업 출현 오프셋(생략했다면 에셋의 값)";

            // 적 종류 자체의 오류는 시트가 아니라 적 종류 에셋의 것이다.
            return path.StartsWith("Enemies", StringComparison.Ordinal) ? "적 종류 에셋 " + path : path;
        }
    }
}
