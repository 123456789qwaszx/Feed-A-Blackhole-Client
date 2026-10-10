#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Globalization;
using BlackHole.Analytics;

namespace BlackHole.Unity
{
    // 값이 원래 사는 곳(승격·되돌리기가 고치는 원본).
    internal enum TargetSource
    {
        NodeCostCsv,
        NodeEffectsCsv,
        BattleRules,
        SkillSetup,
        EnemyKind,
        EnemySupplySetup,
        HqGrowthSetup,
    }

    // 프로필 경로 하나가 가리키는 원본 칸. 에셋 칸은 SerializedProperty 경로로 적는다.
    // 키로 찾는 배열 원소(성질·시작 공급)는 ArrayProperty에서 KeyField가 Key인 원소의 ElementField다.
    internal sealed class ProfileTarget
    {
        public string Path;
        public TargetSource Source;
        public BalanceProfilePatcher.SlotKind Kind;

        // 에셋: 적 종류(EnemyKind 에셋을 고른다. 계약 ID, 예: asteroid). 다른 에셋이면 null.
        public string EnemyKey;
        // 에셋: 바로 찾는 칸(예: timeLimit, tiers.Array.data[0].maxHealth). 키로 찾는 원소면 null.
        public string PropertyPath;
        public string ArrayProperty;
        public string KeyField;
        public string Key;
        public string ElementField;

        // CSV: 노드 ID·Rank·효과 StatId(비용이면 null)·고칠 칸 이름.
        public string NodeId;
        public int Rank;
        public string StatId;
        public string Column;

        public bool IsCsv => Source == TargetSource.NodeCostCsv || Source == TargetSource.NodeEffectsCsv;

        // 사람이 읽는 위치. 예: "NodeEffects.csv growth.asteroids-01 Rank 1 growth.asteroids · Value", "Enemies/asteroid · traits[golden].multiplier"
        public override string ToString()
        {
            switch (Source)
            {
                case TargetSource.NodeCostCsv:
                    return $"NodeCost.csv {NodeId} Rank {Rank} · {Column}";
                case TargetSource.NodeEffectsCsv:
                    return $"NodeEffects.csv {NodeId} Rank {Rank} {StatId} · {Column}";
                case TargetSource.EnemyKind:
                    return $"적 종류 {EnemyKey} · {Field}";
                default:
                    return $"{Source} · {Field}";
            }
        }

        private string Field => PropertyPath ?? $"{ArrayProperty}[{Key}].{ElementField}";
    }

    // 프로필 경로 → 원본 칸 대응표(M4 승격). 경로 문법은 BalanceProfilePatcher와 같다.
    // 노드 경로는 노드 CSV의 그 행, 나머지는 게임 콘텐츠 세트가 가리키는 에셋의 그 칸이다.
    // 에셋 칸이 없는 경로(breaker/planetBonusDamage·starBonusDamage는 노드 효과로만 바뀐다)는 승격하지 않는다.
    internal static class ProfileTargets
    {
        public static bool TryMap(string path, out ProfileTarget target, out string error)
        {
            target = null;
            error = null;

            if (string.IsNullOrEmpty(path))
            {
                error = "경로가 비어 있다.";
                return false;
            }

            string[] p = path.Split('/');

            switch (p[0])
            {
                case "battle" when p.Length == 2 && (p[1] == "timeLimit" || p[1] == "killTimeBonus"):
                    target = Asset(path, TargetSource.BattleRules, p[1], BalanceProfilePatcher.SlotKind.Real);
                    break;

                case "breaker" when p.Length == 2:
                    if (p[1] == "planetBonusDamage" || p[1] == "starBonusDamage")
                    {
                        error = "스킬 설정 에셋에 이 칸이 없다(노드 효과로만 바뀐다). 승격하지 않는다.";
                        return false;
                    }

                    if (!IsOneOf(p[1], "damage", "interval", "radius", "critChance", "critDamage", "moonDuration", "moonSpeedBonus",
                            "moonRadiusBonus", "cometDuration", "cometCritDamageBonus"))
                        break;

                    target = Asset(path, TargetSource.SkillSetup, "breaker" + char.ToUpperInvariant(p[1][0]) + p[1].Substring(1),
                        BalanceProfilePatcher.SlotKind.Real);
                    break;

                case "placement" when p.Length == 2 && (p[1] == "minDistance" || p[1] == "maxDistance"):
                    target = Asset(path, TargetSource.EnemySupplySetup, p[1], BalanceProfilePatcher.SlotKind.Real);
                    break;

                case "enemy" when p.Length >= 3:
                    target = Enemy(path, p);
                    break;

                case "supply" when p.Length == 3 && p[2] == "count":
                    target = new ProfileTarget
                    {
                        Path = path,
                        Source = TargetSource.EnemySupplySetup,
                        Kind = BalanceProfilePatcher.SlotKind.Int,
                        ArrayProperty = "startSupply",
                        KeyField = "kind",
                        Key = p[1],
                        ElementField = "count",
                    };
                    break;

                case "growth" when p.Length == 3 && p[1] == "levelExp" && TryNumber(p[2], out int level):
                    target = Asset(path, TargetSource.HqGrowthSetup, $"levelExp.Array.data[{Index(level)}]", BalanceProfilePatcher.SlotKind.Long);
                    break;

                case "growth" when p.Length == 4 && p[1] == "milestone" && TryNumber(p[2], out int milestone):
                    target = p[3] switch
                    {
                        "level" => Asset(path, TargetSource.HqGrowthSetup, $"milestones.Array.data[{Index(milestone)}].level", BalanceProfilePatcher.SlotKind.Int),
                        "targetGold" => Asset(path, TargetSource.HqGrowthSetup, $"milestones.Array.data[{Index(milestone)}].targetGold", BalanceProfilePatcher.SlotKind.Long),
                        "fieldScale" => Asset(path, TargetSource.HqGrowthSetup, $"milestones.Array.data[{Index(milestone)}].fieldScale", BalanceProfilePatcher.SlotKind.Real),
                        _ => null,
                    };
                    break;

                case "node" when p.Length == 4 && TryNumber(p[2], out int rank):
                    target = p[3] == "cost"
                        ? new ProfileTarget
                        {
                            Path = path,
                            Source = TargetSource.NodeCostCsv,
                            Kind = BalanceProfilePatcher.SlotKind.Long,
                            NodeId = p[1],
                            Rank = rank,
                            Column = NodeSheetEdits.CostColumn,
                        }
                        : new ProfileTarget
                        {
                            Path = path,
                            Source = TargetSource.NodeEffectsCsv,
                            Kind = BalanceProfilePatcher.SlotKind.Real,
                            NodeId = p[1],
                            Rank = rank,
                            StatId = p[3],
                            Column = NodeSheetEdits.ValueColumn,
                        };
                    break;
            }

            if (target == null && error == null)
                error = "원본 칸으로 옮길 수 없는 경로다.";

            return target != null;
        }

        // 열거형 이름(Golden, Asteroid) → 경로에 쓰는 키(golden, asteroid). 에셋의 키 칸(성질 종류·공급 종류)을 비교할 때 쓴다.
        public static string KeyOf(string enumName) => ContractIds.Of(enumName);

        private static ProfileTarget Enemy(string path, string[] p)
        {
            if (p.Length == 3)
            {
                return p[2] switch
                {
                    "moveSpeed" or "radius" or "radiusStep" or "spawnPeriod" =>
                        EnemyField(path, p[1], p[2], BalanceProfilePatcher.SlotKind.Real),
                    "rainCount" => EnemyField(path, p[1], p[2], BalanceProfilePatcher.SlotKind.Int),
                    _ => null,
                };
            }

            if (p.Length == 5 && p[2] == "tier" && TryNumber(p[3], out int tier))
            {
                string at = $"tiers.Array.data[{Index(tier)}].";
                return p[4] switch
                {
                    "hp" => EnemyField(path, p[1], at + "maxHealth", BalanceProfilePatcher.SlotKind.Real),
                    "gold" => EnemyField(path, p[1], at + "gold", BalanceProfilePatcher.SlotKind.Long),
                    "exp" => EnemyField(path, p[1], at + "exp", BalanceProfilePatcher.SlotKind.Long),
                    _ => null,
                };
            }

            if (p.Length == 5 && p[2] == "trait")
            {
                BalanceProfilePatcher.SlotKind? kind = p[4] switch
                {
                    "maxTargets" or "maxActive" => BalanceProfilePatcher.SlotKind.Int,
                    "multiplier" or "critRewardScale" or "damage" or "radius" or "branchChance" or "critChance" or "critMultiplier"
                        or "healthFraction" or "width" => BalanceProfilePatcher.SlotKind.Real,
                    _ => null,
                };

                if (kind == null)
                    return null;

                return new ProfileTarget
                {
                    Path = path,
                    Source = TargetSource.EnemyKind,
                    Kind = kind.Value,
                    EnemyKey = p[1],
                    ArrayProperty = "traits",
                    KeyField = "type",
                    Key = p[3],
                    ElementField = p[4],
                };
            }

            return null;
        }

        private static ProfileTarget Asset(string path, TargetSource source, string property, BalanceProfilePatcher.SlotKind kind) =>
            new ProfileTarget { Path = path, Source = source, PropertyPath = property, Kind = kind };

        private static ProfileTarget EnemyField(string path, string enemy, string property, BalanceProfilePatcher.SlotKind kind) =>
            new ProfileTarget { Path = path, Source = TargetSource.EnemyKind, EnemyKey = enemy, PropertyPath = property, Kind = kind };

        private static bool TryNumber(string text, out int number) =>
            int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number) && number >= 1;

        private static string Index(int number) => (number - 1).ToString(CultureInfo.InvariantCulture);

        private static bool IsOneOf(string value, params string[] options)
        {
            foreach (string option in options)
            {
                if (option == value)
                    return true;
            }

            return false;
        }
    }
}
#endif
