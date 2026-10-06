using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BlackHole.Core;
using static BlackHole.Authoring.SheetCells;

namespace BlackHole.Authoring
{
    // 적 종류(ContentData.Enemies.Enemies)와 데이터 시트의 세 탭 사이의 변환. 한 종류가 세 탭에 걸쳐 있고 kind(ID)로 잇는다.
    // - Enemies 탭: 한 행이 종류 하나. id | moveSpeed | radius | radiusStep | upgradesTo | pickupPeriod
    //   radius는 크기 1의 반지름, radiusStep은 크기가 1 오를 때 늘어나는 반지름(크기 1 대비, 0 이상).
    //   upgradesTo는 다른 종류의 ID이고 비우면 없다. pickupPeriod는 픽업(혜성)의 등장 주기(초)이고 0이면 공급되는 보통 종류다.
    // - EnemyTiers 탭: kind | tier | color | maxHealth | gold | exp. tier는 종류마다 0부터 차례로(공급되는 종류는 7색). color는 #RRGGBB(#RRGGBBAA).
    //   색 분포는 질량 노드(enemy.<종류>.mass), 크기는 크기 노드(enemy.<종류>.size)가 정한다 — 시트에 칸이 없다.
    // - EnemyTraits 탭: kind | trait | color | effect | multiplier | damage | radius | maxTargets | branchChance | critChance | critMultiplier
    //   | healthFraction | width. 한 행이 그 종류에 붙을 수 있는 특수 성질 하나(황금·전기·달 …). 성질이 없는 종류는 행을 두지 않는다.
    //   color는 화면 표식의 색(#RRGGBB(#RRGGBBAA)). 효과 칸은 그 효과가 쓰는 것만 채운다(쓰지 않는 칸에 값이 있으면 오류).
    //   생성 확률은 시트가 아니라 노드(enemy.<종류>.trait.<성질>.chance)다 — 기본 0%.
    // 종류의 목록과 순서는 적 종류 목록 에셋이 정한다: 시트는 그 종류를 모두, 그 종류만 담는다(종류를 더하고 빼는 것은 에셋에서 한다).
    // 규칙은 게임과 같은 로더(EnemyContentLoader)로 본다.
    public static class EnemySheet
    {
        public const string EnemiesTab = "Enemies";
        public const string TiersTab = "EnemyTiers";
        public const string TraitsTab = "EnemyTraits";

        private static readonly string[] _enemyColumns =
        {
            "id", "moveSpeed", "radius", "radiusStep", "upgradesTo", "pickupPeriod",
        };

        private static readonly string[] _traitColumns =
        {
            "kind", "trait", "color", "effect", "multiplier", "damage", "radius", "maxTargets", "branchChance", "critChance", "critMultiplier",
            "healthFraction", "width",
        };

        private const int TraitEffectColumn = 3;

        private static readonly string[] _tierColumns = { "kind", "tier", "color", "maxHealth", "gold", "exp" };

        // 사망 효과마다 쓰는 효과 칸(EnemyTraits 탭의 머리칸 이름). 정의 생성자의 매개변수 이름과 같다.
        private static readonly Dictionary<string, string[]> _effectParameters = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Golden"] = new[] { "multiplier" },
            ["ChainLightning"] = new[] { "damage", "radius", "maxTargets", "branchChance", "critChance", "critMultiplier" },
            ["Explosion"] = new[] { "healthFraction", "radius" },
            ["LaserBurst"] = new[] { "damage", "width", "critChance", "critMultiplier" },
            // 버프는 쓰는 칸이 없다: 시간과 수치는 Breaker의 것이다(Skills 탭).
            ["MoonBuff"] = Array.Empty<string>(),
            ["CometBuff"] = Array.Empty<string>(),
        };

        // 효과 칸 이름 → EnemyTraits 탭 열 번호.
        private static readonly Dictionary<string, int> _effectColumns = EffectColumns();

        private static Dictionary<string, int> EffectColumns()
        {
            var columns = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int c = TraitEffectColumn + 1; c < _traitColumns.Length; c++)
                columns.Add(_traitColumns[c], c);

            return columns;
        }

        private static readonly Regex _hex = new Regex("^#?([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$");

        // 종류마다 시트 행 번호. 규칙 진단을 시트 위치로 바꿀 때 쓴다.
        private sealed class Rows
        {
            public int Enemy;
            public readonly List<int> Tiers = new List<int>();
            // 성질 ID → 시트 행. 로더의 경로는 성질을 ID로 가리킨다(Traits[electric]).
            public readonly Dictionary<string, int> Traits = new Dictionary<string, int>(StringComparer.Ordinal);
            public readonly List<int> TraitRows = new List<int>();
        }

        // colorOf(종류 ID, 색 등급) → "#RRGGBB" 또는 "#RRGGBBAA".
        public static string EnemiesCsv(IReadOnlyList<EnemyData> enemies)
        {
            var rows = new List<IReadOnlyList<string>> { _enemyColumns };

            foreach (EnemyData enemy in enemies)
            {
                rows.Add(new[]
                {
                    enemy.Id, Number(enemy.MoveSpeed), Number(enemy.Radius), Number(enemy.RadiusStep), enemy.UpgradesTo ?? string.Empty,
                    Number(enemy.PickupPeriod),
                });
            }

            return Csv.Write(rows);
        }

        public static string TiersCsv(IReadOnlyList<EnemyData> enemies, Func<string, int, string> colorOf)
        {
            var rows = new List<IReadOnlyList<string>> { _tierColumns };

            foreach (EnemyData enemy in enemies)
            {
                for (int i = 0; i < enemy.Tiers.Count; i++)
                {
                    EnemyTierData tier = enemy.Tiers[i];
                    rows.Add(new[]
                    {
                        enemy.Id, Number(i), colorOf(enemy.Id, i), Number(tier.MaxHealth), Number(tier.Gold), Number(tier.Exp),
                    });
                }
            }

            return Csv.Write(rows);
        }

        // colorOf(종류 ID, 성질 번호) → "#RRGGBB" 또는 "#RRGGBBAA".
        public static string TraitsCsv(IReadOnlyList<EnemyData> enemies, Func<string, int, string> colorOf)
        {
            var rows = new List<IReadOnlyList<string>> { _traitColumns };

            foreach (EnemyData enemy in enemies)
            {
                for (int i = 0; i < enemy.Traits.Count; i++)
                {
                    EnemyTraitData trait = enemy.Traits[i];
                    DeathEffectData effect = trait.Effect;
                    string kind = effect?.Kind ?? string.Empty;
                    string[] used = _effectParameters.TryGetValue(kind, out string[] names) ? names : Array.Empty<string>();
                    var row = new List<string> { enemy.Id, trait.Id, colorOf(enemy.Id, i), kind };

                    for (int c = TraitEffectColumn + 1; c < _traitColumns.Length; c++)
                        row.Add(Used(used, _traitColumns[c]) ? EffectValue(effect, _traitColumns[c]) : string.Empty);

                    rows.Add(row);
                }
            }

            return Csv.Write(rows);
        }

        private static string EffectValue(DeathEffectData effect, string parameter)
        {
            switch (parameter)
            {
                case "multiplier": return Number(effect.Multiplier);
                case "damage": return Number(effect.Damage);
                case "radius": return Number(effect.Radius);
                case "maxTargets": return Number(effect.MaxTargets);
                case "branchChance": return Number(effect.BranchChance);
                case "critChance": return Number(effect.CritChance);
                case "critMultiplier": return Number(effect.CritMultiplier);
                case "healthFraction": return Number(effect.HealthFraction);
                case "width": return Number(effect.Width);
                default: throw new ArgumentOutOfRangeException(nameof(parameter), parameter);
            }
        }

        // 세 탭을 읽어 통과하면 into.Enemies.Enemies를 바꾸고 colors에 종류마다의 색 등급 색, traitColors에 성질 색(#RRGGBB)을 채운다.
        // 반환: 진단(없으면 통과). 종류의 목록과 순서는 into.Enemies.Enemies(적 종류 목록 에셋)의 것이다.
        public static List<ContentDiagnostic> Read(string enemiesCsv, string tiersCsv, string traitsCsv, ContentData into,
            Dictionary<string, List<string>> colors, Dictionary<string, List<string>> traitColors)
        {
            var diagnostics = new List<ContentDiagnostic>();
            var byId = new Dictionary<string, EnemyData>(StringComparer.Ordinal);
            var rows = new Dictionary<string, Rows>(StringComparer.Ordinal);
            var sheetColors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var sheetTraitColors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var order = new List<string>();

            foreach (EnemyData existing in into.Enemies.Enemies)
                order.Add(existing.Id);

            ReadEnemies(Csv.Parse(enemiesCsv ?? string.Empty), order, byId, rows, diagnostics);
            ReadTiers(Csv.Parse(tiersCsv ?? string.Empty), byId, rows, sheetColors, diagnostics);
            ReadTraits(Csv.Parse(traitsCsv ?? string.Empty), byId, rows, sheetTraitColors, diagnostics);

            if (diagnostics.Count > 0)
                return diagnostics;

            var enemies = new List<EnemyData>();

            foreach (string id in order)
                enemies.Add(byId[id]);

            var check = new EnemyContentData
            {
                Enemies = enemies,
                EnemyPlacement = into.Enemies.EnemyPlacement,
                PickupPlacement = into.Enemies.PickupPlacement,
                MaxAliveEnemies = into.Enemies.MaxAliveEnemies,
                StartSupply = into.Enemies.StartSupply,
            };
            var rules = new List<ContentDiagnostic>();
            EnemyContentLoader.Load(check, rules);

            foreach (ContentDiagnostic rule in rules)
                diagnostics.Add(new ContentDiagnostic(PlaceOf(rule, rows), RuleMessage(rule.Message)));

            if (diagnostics.Count > 0)
                return diagnostics;

            into.Enemies.Enemies = enemies;

            foreach (KeyValuePair<string, List<string>> pair in sheetColors)
                colors[pair.Key] = pair.Value;

            foreach (EnemyData enemy in enemies)
                traitColors[enemy.Id] = sheetTraitColors.TryGetValue(enemy.Id, out List<string> list) ? list : new List<string>();

            return diagnostics;
        }

        private static void ReadEnemies(List<string[]> table, List<string> order, Dictionary<string, EnemyData> byId,
            Dictionary<string, Rows> rows, List<ContentDiagnostic> diagnostics)
        {
            if (!HasHeader(table, EnemiesTab, _enemyColumns, diagnostics))
                return;

            var known = new HashSet<string>(order, StringComparer.Ordinal);

            for (int r = 1; r < table.Count; r++)
            {
                string[] row = table[r];
                int sheetRow = r + 1;

                if (IsBlank(row, _enemyColumns.Length - 1))
                    continue;

                string id = Text(row, 0);

                if (!known.Contains(id))
                {
                    diagnostics.Add(new ContentDiagnostic(Cell(EnemiesTab, 0, sheetRow),
                        $"적 종류 목록 에셋에 없는 ID다: '{id}'. 종류를 더하려면 먼저 EnemyKind 에셋을 만들어 목록에 넣는다."));
                    continue;
                }

                if (rows.TryGetValue(id, out Rows first))
                {
                    diagnostics.Add(new ContentDiagnostic(Cell(EnemiesTab, 0, sheetRow), $"'{id}'가 {first.Enemy}행에도 있다."));
                    continue;
                }

                var enemy = new EnemyData
                {
                    Id = id,
                    MoveSpeed = ReadFloat(row, 1, EnemiesTab, sheetRow, diagnostics),
                    Radius = ReadFloat(row, 2, EnemiesTab, sheetRow, diagnostics),
                    RadiusStep = ReadFloat(row, 3, EnemiesTab, sheetRow, diagnostics),
                    UpgradesTo = OrNull(Text(row, 4)),
                    PickupPeriod = ReadFloat(row, 5, EnemiesTab, sheetRow, diagnostics),
                };

                byId.Add(id, enemy);
                rows.Add(id, new Rows { Enemy = sheetRow });
            }

            foreach (string id in order)
            {
                if (!byId.ContainsKey(id) && !HasError(diagnostics, EnemiesTab))
                    diagnostics.Add(new ContentDiagnostic(EnemiesTab, $"'{id}' 행이 없다. 적 종류 목록 에셋의 종류는 모두 적는다."));
            }
        }

        private static void ReadTraits(List<string[]> table, Dictionary<string, EnemyData> byId, Dictionary<string, Rows> rows,
            Dictionary<string, List<string>> colors, List<ContentDiagnostic> diagnostics)
        {
            if (!HasHeader(table, TraitsTab, _traitColumns, diagnostics))
                return;

            for (int r = 1; r < table.Count; r++)
            {
                string[] row = table[r];
                int sheetRow = r + 1;

                if (IsBlank(row, _traitColumns.Length - 1) || !TryKind(row, TraitsTab, sheetRow, byId, diagnostics, out EnemyData enemy))
                    continue;

                string id = Text(row, 1);
                Rows at = rows[enemy.Id];

                if (id.Length == 0)
                {
                    diagnostics.Add(new ContentDiagnostic(Cell(TraitsTab, 1, sheetRow), "성질 ID가 비어 있다."));
                    continue;
                }

                if (at.Traits.TryGetValue(id, out int first))
                {
                    diagnostics.Add(new ContentDiagnostic(Cell(TraitsTab, 1, sheetRow), $"'{enemy.Id}'의 성질 '{id}'가 {first}행에도 있다."));
                    continue;
                }

                string color = Text(row, 2);

                if (!_hex.IsMatch(color))
                    diagnostics.Add(new ContentDiagnostic(Cell(TraitsTab, 2, sheetRow), $"색은 #RRGGBB 또는 #RRGGBBAA로 적는다. 받은 값: '{color}'."));

                enemy.Traits.Add(new EnemyTraitData { Id = id, Effect = ReadEffect(row, sheetRow, diagnostics) });

                if (!colors.TryGetValue(enemy.Id, out List<string> list))
                    colors.Add(enemy.Id, list = new List<string>());

                list.Add(NormalizedColor(color));
                at.Traits.Add(id, sheetRow);
                at.TraitRows.Add(sheetRow);
            }
        }

        private static DeathEffectData ReadEffect(string[] row, int sheetRow, List<ContentDiagnostic> diagnostics)
        {
            string kind = Text(row, TraitEffectColumn);

            if (!_effectParameters.TryGetValue(kind, out string[] used))
            {
                diagnostics.Add(new ContentDiagnostic(Cell(TraitsTab, TraitEffectColumn, sheetRow),
                    kind.Length == 0
                        ? $"성질에는 효과가 있어야 한다. 쓸 수 있는 값: {string.Join(", ", _effectParameters.Keys)}."
                        : $"알 수 없는 사망 효과 '{kind}'. 쓸 수 있는 값: {string.Join(", ", _effectParameters.Keys)}."));
                return null;
            }

            foreach (KeyValuePair<string, int> column in _effectColumns)
            {
                if (!Used(used, column.Key) && Text(row, column.Value).Length > 0)
                    diagnostics.Add(new ContentDiagnostic(Cell(TraitsTab, column.Value, sheetRow), $"{kind}는 이 칸을 쓰지 않는다. 비운다."));
            }

            float Float(string parameter) =>
                Used(used, parameter) ? ReadFloat(row, _effectColumns[parameter], TraitsTab, sheetRow, diagnostics) : 0;

            return new DeathEffectData
            {
                Kind = kind,
                Multiplier = Float("multiplier"),
                Damage = Float("damage"),
                Radius = Float("radius"),
                MaxTargets = Used(used, "maxTargets") ? ReadInt(row, _effectColumns["maxTargets"], TraitsTab, sheetRow, diagnostics) : 0,
                BranchChance = Float("branchChance"),
                CritChance = Float("critChance"),
                CritMultiplier = Float("critMultiplier"),
                HealthFraction = Float("healthFraction"),
                Width = Float("width"),
            };
        }

        private static void ReadTiers(List<string[]> table, Dictionary<string, EnemyData> byId, Dictionary<string, Rows> rows,
            Dictionary<string, List<string>> colors, List<ContentDiagnostic> diagnostics)
        {
            if (!HasHeader(table, TiersTab, _tierColumns, diagnostics))
                return;

            var lastTier = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int r = 1; r < table.Count; r++)
            {
                string[] row = table[r];
                int sheetRow = r + 1;

                if (IsBlank(row, _tierColumns.Length - 1) || !TryKind(row, TiersTab, sheetRow, byId, diagnostics, out EnemyData enemy))
                    continue;

                CheckOrder(row, TiersTab, sheetRow, enemy.Id, lastTier, "tier", diagnostics);

                string color = Text(row, 2);

                if (!_hex.IsMatch(color))
                    diagnostics.Add(new ContentDiagnostic(Cell(TiersTab, 2, sheetRow), $"색은 #RRGGBB 또는 #RRGGBBAA로 적는다. 받은 값: '{color}'."));

                enemy.Tiers.Add(new EnemyTierData
                {
                    MaxHealth = ReadFloat(row, 3, TiersTab, sheetRow, diagnostics),
                    Gold = ReadLong(row, 4, TiersTab, sheetRow, diagnostics),
                    Exp = ReadLong(row, 5, TiersTab, sheetRow, diagnostics),
                });

                if (!colors.TryGetValue(enemy.Id, out List<string> list))
                    colors.Add(enemy.Id, list = new List<string>());

                list.Add(NormalizedColor(color));
                rows[enemy.Id].Tiers.Add(sheetRow);
            }
        }

        // 행의 kind가 Enemies 탭에 있는 종류인가. Enemies 탭에서 빠졌거나 틀린 종류는 거기서 이미 알렸으므로 여기서는 없는 ID만 알린다.
        private static bool TryKind(string[] row, string tab, int sheetRow, Dictionary<string, EnemyData> byId, List<ContentDiagnostic> diagnostics,
            out EnemyData enemy)
        {
            string id = Text(row, 0);

            if (byId.TryGetValue(id, out enemy))
                return true;

            if (!HasError(diagnostics, EnemiesTab))
                diagnostics.Add(new ContentDiagnostic(Cell(tab, 0, sheetRow), $"Enemies 탭에 없는 종류다: '{id}'."));

            return false;
        }

        // 두 번째 칸(tier·level)이 그 종류의 바로 앞 행 번호 + 1인가(처음은 0). 틀려도 그 행은 읽는다:
        // 다음 행은 이 행의 번호에 이어 보므로, 번호 하나가 틀리면 오류도 하나다.
        private static void CheckOrder(string[] row, string tab, int sheetRow, string id, Dictionary<string, int> last, string label,
            List<ContentDiagnostic> diagnostics)
        {
            int before = diagnostics.Count;
            int index = ReadInt(row, 1, tab, sheetRow, diagnostics);
            int expected = last.TryGetValue(id, out int previous) ? previous + 1 : 0;
            bool read = diagnostics.Count == before;

            if (read && index != expected)
                diagnostics.Add(new ContentDiagnostic(Cell(tab, 1, sheetRow), $"{label} 칸은 종류마다 0부터 차례로 적는다. 여기는 {expected}이어야 한다."));

            last[id] = read ? index : expected;
        }

        // 로더의 경로("Enemies[asteroid].Tiers[2]")와 문장의 매개변수 이름으로 시트 위치를 찾는다.
        private static string PlaceOf(ContentDiagnostic rule, Dictionary<string, Rows> rows)
        {
            string path = rule.Path;
            string parameter = ParameterOf(rule.Message);

            if (!path.StartsWith("Enemies[", StringComparison.Ordinal))
                return path;

            int end = path.IndexOf(']');
            string id = end > 0 ? path.Substring(8, end - 8) : string.Empty;

            if (!rows.TryGetValue(id, out Rows at))
                return path;

            string rest = path.Substring(end + 1);

            if (TryIndex(rest, ".Tiers[", out int tier, out _) && tier < at.Tiers.Count)
                return Cell(TiersTab, ColumnOf(_tierColumns, parameter), at.Tiers[tier]);

            // 공급되는 종류의 색 수(EnemyContentInvariants).
            if (rest == ".Tiers")
                return Span(TiersTab, _tierColumns.Length - 1, at.Tiers, id);

            if (TryTrait(rest, out string traitId, out string traitRest) && at.Traits.TryGetValue(traitId, out int traitRow))
            {
                if (traitRest == ".Effect.Kind")
                    return Cell(TraitsTab, TraitEffectColumn, traitRow);

                if (traitRest == ".Effect")
                    return Cell(TraitsTab, parameter != null && _effectColumns.TryGetValue(parameter, out int column) ? column : TraitEffectColumn, traitRow);

                return Cell(TraitsTab, 1, traitRow);
            }

            if (rest == ".UpgradesTo")
                return Cell(EnemiesTab, Array.IndexOf(_enemyColumns, "upgradesTo"), at.Enemy);

            // 종류 전체의 규칙(EnemyDefinition 생성자): 매개변수가 가리키는 탭·칸.
            switch (parameter)
            {
                case "tiers": return Span(TiersTab, _tierColumns.Length - 1, at.Tiers, id);
                case "traits": return Span(TraitsTab, _traitColumns.Length - 1, at.TraitRows, id);
            }

            int enemyColumn = parameter == null ? -1 : Array.IndexOf(_enemyColumns, parameter);
            return enemyColumn >= 0 ? Cell(EnemiesTab, enemyColumn, at.Enemy) : Range(EnemiesTab, 0, _enemyColumns.Length - 1, at.Enemy);
        }

        // ".Traits[electric].Effect" → ("electric", ".Effect").
        private static bool TryTrait(string rest, out string id, out string after)
        {
            const string prefix = ".Traits[";
            id = null;
            after = null;

            if (!rest.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            int end = rest.IndexOf(']', prefix.Length);

            if (end < 0)
                return false;

            id = rest.Substring(prefix.Length, end - prefix.Length);
            after = rest.Substring(end + 1);
            return true;
        }

        // 한 종류의 여러 행: 첫 행부터 끝 행까지. 행이 없으면 탭 이름과 종류.
        private static string Span(string tab, int lastColumn, List<int> sheetRows, string id)
        {
            if (sheetRows.Count == 0)
                return $"{tab}('{id}' 행 없음)";

            return $"{tab}!A{sheetRows[0]}:{Column(lastColumn)}{sheetRows[sheetRows.Count - 1]}";
        }

        // 매개변수 이름과 같은 머리칸의 열. 없으면 첫 열(kind).
        private static int ColumnOf(string[] columns, string parameter) => parameter == null ? 0 : Math.Max(0, Array.IndexOf(columns, parameter));

        // "#rrggbb" → "#RRGGBB". 불투명(FF)이면 알파를 뗀다: 내보내기와 같은 글자로 맞춰 값이 같은지 비교할 수 있게.
        private static string NormalizedColor(string color)
        {
            string hex = color.TrimStart('#').ToUpperInvariant();
            return "#" + (hex.Length == 8 && hex.EndsWith("FF", StringComparison.Ordinal) ? hex.Substring(0, 6) : hex);
        }

        private static bool Used(string[] used, string parameter) => Array.IndexOf(used, parameter) >= 0;

        private static string OrNull(string text) => text.Length == 0 ? null : text;

        private static bool HasError(List<ContentDiagnostic> diagnostics, string tab)
        {
            foreach (ContentDiagnostic diagnostic in diagnostics)
            {
                if (diagnostic.Path.StartsWith(tab, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
