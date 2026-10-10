#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using BlackHole.Analytics;
using BlackHole.Core;

namespace BlackHole.Unity
{
    // 밸런스 프로필의 패치를 콘텐츠 저작 데이터(ContentData)와 노드 시트 데이터(NodeContentData)에 적용한다.
    // 로더(ContentLoader·NodeContentLoader)에 넘기기 전에 부르므로, 패치한 값도 원래 검증을 그대로 거친다.
    //
    // 경로는 슬래시로 나눈다. 종류·성질은 계약 ID처럼 열거형 이름의 첫 글자만 소문자다(asteroid, golden).
    //   battle/<timeLimit|killTimeBonus>
    //   breaker/<damage|interval|radius|critChance|critDamage|moonDuration|moonSpeedBonus|moonRadiusBonus|cometDuration|cometCritDamageBonus|planetBonusDamage|starBonusDamage>
    //   enemy/<종류>/<moveSpeed|radius|radiusStep|spawnPeriod|rainCount>
    //   enemy/<종류>/tier/<색 등급, 1부터>/<hp|gold|exp>
    //   enemy/<종류>/trait/<성질>/<multiplier|critRewardScale|damage|radius|maxTargets|branchChance|critChance|critMultiplier|healthFraction|width|maxActive>
    //   supply/<종류>/count                       시작 공급에 없는 종류면 더한다
    //   growth/levelExp/<Level, 1부터>             그 Level에 닿는 누적 EXP
    //   growth/milestone/<번호, 1부터>/<level|targetGold|fieldScale>
    //   node/<노드 ID>/<Rank>/cost
    //   node/<노드 ID>/<Rank>/<StatId>             그 Rank의 효과 값
    // 모든 패치를 먼저 검사하고, 하나라도 틀리면 아무것도 바꾸지 않는다(부분 적용 없음).
    internal static class BalanceProfilePatcher
    {
        public static IReadOnlyList<string> Apply(BalanceProfile profile, ContentData content, NodeContentData nodes)
        {
            var errors = new List<string>();
            var setters = new List<Action>();

            for (int i = 0; i < profile.patches.Count; i++)
            {
                BalanceProfile.Patch patch = profile.patches[i];
                string path = patch?.path;

                if (string.IsNullOrEmpty(path))
                {
                    errors.Add($"patches[{i}]: 경로가 비어 있다.");
                    continue;
                }

                Action setter = Resolve(path.Split('/'), patch.value, content, nodes, out string error);

                if (setter == null)
                    errors.Add($"patches[{i}] '{path}': {error}");
                else
                    setters.Add(setter);
            }

            if (errors.Count == 0)
            {
                foreach (Action setter in setters)
                    setter();
            }

            return errors;
        }

        private static Action Resolve(string[] path, double value, ContentData content, NodeContentData nodes, out string error)
        {
            switch (path[0])
            {
                case "battle" when path.Length == 2:
                    return Battle(path[1], value, content.BattleRules, out error);
                case "breaker" when path.Length == 2:
                    return Breaker(path[1], value, content.Breaker, out error);
                case "enemy":
                    return Enemy(path, value, content.Enemies, out error);
                case "supply" when path.Length == 3 && path[2] == "count":
                    return Supply(path[1], value, content.Enemies, out error);
                case "growth":
                    return Growth(path, value, content.Growth, out error);
                case "node" when path.Length == 4:
                    return Node(path[1], path[2], path[3], value, nodes, out error);
                default:
                    error = "알 수 없는 경로다.";
                    return null;
            }
        }

        private static Action Battle(string field, double value, BattleRulesData rules, out string error)
        {
            switch (field)
            {
                case "timeLimit": return Real(value, v => rules.TimeLimit = v, out error);
                case "killTimeBonus": return Real(value, v => rules.KillTimeBonus = v, out error);
                default: return Unknown(field, out error);
            }
        }

        private static Action Breaker(string field, double value, BreakerData breaker, out string error)
        {
            switch (field)
            {
                case "damage": return Real(value, v => breaker.Damage = v, out error);
                case "interval": return Real(value, v => breaker.Interval = v, out error);
                case "radius": return Real(value, v => breaker.Radius = v, out error);
                case "critChance": return Real(value, v => breaker.CritChance = v, out error);
                case "critDamage": return Real(value, v => breaker.CritDamage = v, out error);
                case "moonDuration": return Real(value, v => breaker.MoonDuration = v, out error);
                case "moonSpeedBonus": return Real(value, v => breaker.MoonSpeedBonus = v, out error);
                case "moonRadiusBonus": return Real(value, v => breaker.MoonRadiusBonus = v, out error);
                case "cometDuration": return Real(value, v => breaker.CometDuration = v, out error);
                case "cometCritDamageBonus": return Real(value, v => breaker.CometCritDamageBonus = v, out error);
                case "planetBonusDamage": return Real(value, v => breaker.PlanetBonusDamage = v, out error);
                case "starBonusDamage": return Real(value, v => breaker.StarBonusDamage = v, out error);
                default: return Unknown(field, out error);
            }
        }

        private static Action Enemy(string[] path, double value, EnemyContentData enemies, out string error)
        {
            if (path.Length < 3)
                return Unknown(string.Join("/", path), out error);

            EnemyData enemy = enemies.Enemies.Find(e => ContractIds.Of(e.Type.ToString()) == path[1]);

            if (enemy == null)
            {
                error = $"적 종류가 없다: '{path[1]}'.";
                return null;
            }

            if (path.Length == 3)
            {
                switch (path[2])
                {
                    case "moveSpeed": return Real(value, v => enemy.MoveSpeed = v, out error);
                    case "radius": return Real(value, v => enemy.Radius = v, out error);
                    case "radiusStep": return Real(value, v => enemy.RadiusStep = v, out error);
                    case "spawnPeriod": return Real(value, v => enemy.SpawnPeriod = v, out error);
                    case "rainCount": return Int(value, v => enemy.RainCount = v, out error);
                    default: return Unknown(path[2], out error);
                }
            }

            if (path.Length == 5 && path[2] == "tier")
            {
                if (!TryIndex(path[3], enemy.Tiers.Count, out int index, out error))
                    return null;

                EnemyTierData tier = enemy.Tiers[index];

                switch (path[4])
                {
                    case "hp": return Real(value, v => tier.MaxHealth = v, out error);
                    case "gold": return Long(value, v => tier.Gold = v, out error);
                    case "exp": return Long(value, v => tier.Exp = v, out error);
                    default: return Unknown(path[4], out error);
                }
            }

            if (path.Length == 5 && path[2] == "trait")
            {
                EnemyTraitData trait = enemy.Traits.Find(t => ContractIds.Of(t.Type.ToString()) == path[3]);

                if (trait == null)
                {
                    error = $"'{path[1]}'에 그 성질이 없다: '{path[3]}'.";
                    return null;
                }

                if (path[4] == "maxActive")
                    return Int(value, v => trait.MaxActive = v, out error);

                DeathEffectData effect = trait.Effect;

                if (effect == null)
                {
                    error = "성질에 사망 효과 데이터가 없다.";
                    return null;
                }

                switch (path[4])
                {
                    case "multiplier": return Real(value, v => effect.Multiplier = v, out error);
                    case "critRewardScale": return Real(value, v => effect.CritRewardScale = v, out error);
                    case "damage": return Real(value, v => effect.Damage = v, out error);
                    case "radius": return Real(value, v => effect.Radius = v, out error);
                    case "maxTargets": return Int(value, v => effect.MaxTargets = v, out error);
                    case "branchChance": return Real(value, v => effect.BranchChance = v, out error);
                    case "critChance": return Real(value, v => effect.CritChance = v, out error);
                    case "critMultiplier": return Real(value, v => effect.CritMultiplier = v, out error);
                    case "healthFraction": return Real(value, v => effect.HealthFraction = v, out error);
                    case "width": return Real(value, v => effect.Width = v, out error);
                    default: return Unknown(path[4], out error);
                }
            }

            return Unknown(string.Join("/", path), out error);
        }

        private static Action Supply(string kind, double value, EnemyContentData enemies, out string error)
        {
            EnemyType? type = null;

            foreach (EnemyType candidate in Enum.GetValues(typeof(EnemyType)))
            {
                if (ContractIds.Of(candidate.ToString()) == kind)
                    type = candidate;
            }

            if (type == null)
            {
                error = $"적 종류가 없다: '{kind}'.";
                return null;
            }

            SupplyData supply = enemies.StartSupply.Find(s => s.Enemy == type);

            return Int(value, v =>
            {
                if (supply != null)
                    supply.Count = v;
                else
                    enemies.StartSupply.Add(new SupplyData { Enemy = type, Count = v });
            }, out error);
        }

        private static Action Growth(string[] path, double value, HqGrowthData growth, out string error)
        {
            if (path.Length == 3 && path[1] == "levelExp")
            {
                if (!TryIndex(path[2], growth.LevelExp.Count, out int index, out error))
                    return null;

                return Long(value, v => growth.LevelExp[index] = v, out error);
            }

            if (path.Length == 4 && path[1] == "milestone")
            {
                if (!TryIndex(path[2], growth.Milestones.Count, out int index, out error))
                    return null;

                HqMilestoneData milestone = growth.Milestones[index];

                switch (path[3])
                {
                    case "level": return Int(value, v => milestone.Level = v, out error);
                    case "targetGold": return Long(value, v => milestone.TargetGold = v, out error);
                    case "fieldScale": return Real(value, v => milestone.FieldScale = v, out error);
                    default: return Unknown(path[3], out error);
                }
            }

            return Unknown(string.Join("/", path), out error);
        }

        private static Action Node(string nodeId, string rankText, string field, double value, NodeContentData nodes, out string error)
        {
            if (!int.TryParse(rankText, NumberStyles.None, CultureInfo.InvariantCulture, out int rank))
            {
                error = $"Rank는 1 이상의 정수다: '{rankText}'.";
                return null;
            }

            if (field == "cost")
            {
                NodeCostRowData cost = nodes.Costs.Find(c => c.NodeId == nodeId && c.Rank == rank);

                if (cost == null)
                {
                    error = $"노드 가격 행이 없다: '{nodeId}' Rank {rank}.";
                    return null;
                }

                return Long(value, v => cost.Cost = v, out error);
            }

            NodeEffectRowData effect = nodes.Effects.Find(e => e.NodeId == nodeId && e.Rank == rank && e.StatId == field);

            if (effect == null)
            {
                error = $"노드 효과 행이 없다: '{nodeId}' Rank {rank} '{field}'.";
                return null;
            }

            return Real(value, v => effect.Value = v, out error);
        }

        // 1부터 센 번호 text를 0부터의 인덱스로. 범위 밖이면 false.
        private static bool TryIndex(string text, int count, out int index, out string error)
        {
            if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int number) && number >= 1 && number <= count)
            {
                index = number - 1;
                error = null;
                return true;
            }

            index = -1;
            error = $"번호는 1부터 {count}까지다: '{text}'.";
            return false;
        }

        private static Action Real(double value, Action<float> set, out string error)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) > float.MaxValue)
            {
                error = $"유한한 실수가 필요하다: {value}.";
                return null;
            }

            error = null;
            return () => set((float)value);
        }

        private static Action Long(double value, Action<long> set, out string error)
        {
            // double이 정수를 정확히 담는 범위(2^53)까지만 받는다.
            if (value != Math.Floor(value) || Math.Abs(value) > 9007199254740992d)
            {
                error = $"정수가 필요하다: {value}.";
                return null;
            }

            error = null;
            return () => set((long)value);
        }

        private static Action Int(double value, Action<int> set, out string error)
        {
            if (value != Math.Floor(value) || value < int.MinValue || value > int.MaxValue)
            {
                error = $"정수가 필요하다: {value}.";
                return null;
            }

            error = null;
            return () => set((int)value);
        }

        private static Action Unknown(string field, out string error)
        {
            error = $"알 수 없는 칸이다: '{field}'.";
            return null;
        }
    }
}
#endif
