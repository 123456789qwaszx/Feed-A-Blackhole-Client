using System;
using System.Globalization;
using BlackHole.Analytics;
using BlackHole.Core;

namespace BlackHole.Unity
{
    // 게임의 판(GameSession·BattleRawData)을 통계 계약(BattleSummaryDto)으로 옮긴다.
    // 판을 시작할 때 시작 조건을 채우고(Begin), 끝날 때 결과를 채운다(Complete).
    // 시작 조건은 시작할 때 읽어야 한다 — 판이 끝나면 BattleSystem이 판(Session)을 버린다.
    internal static class BattleSummaryBuilder
    {
        public static BattleSummaryDto Begin(
            GameSession session,
            ProgressState progress,
            string installId,
            int battleIndex,
            string buildVersion,
            string platform,
            DateTime startedAtUtc)
        {
            BattleSummaryDto summary = new()
            {
                battleId = Guid.NewGuid().ToString(),
                installId = installId,
                battleIndex = battleIndex,
                buildVersion = buildVersion,
                // 콘텐츠 버전을 정하는 방식이 생기기 전까지 비워 둔다(계약 참고).
                contentVersion = "",
                platform = platform,
                startedAtUtc = Timestamp(startedAtUtc),
                startGrowthStage = session.World.Hq.Stage,
            };

            foreach (string nodeId in progress.OwnedNodes)
            {
                NodeRankDto node = new() { nodeId = nodeId, rank = progress.RankOf(nodeId) };
                summary.nodes.Add(node);
            }

            FillApplied(summary.appliedStats, session);
            return summary;
        }

        public static void Complete(BattleSummaryDto summary, BattleRawData raw, DateTime endedAtUtc)
        {
            summary.endedAtUtc = Timestamp(endedAtUtc);
            summary.playedSeconds = raw.PlayedSeconds;
            summary.seed = raw.Seed;

            foreach (EnemyKillCount kill in raw.Kills)
            {
                EnemyKillDto dto = new() { enemyId = ContractIds.Of(kill.Enemy.Type.ToString()), count = kill.Count };
                summary.kills.Add(dto);
            }

            summary.totalKills = raw.TotalKills;
            summary.earnedGold = raw.EarnedGold;
            summary.settledGold = raw.SettledGold;
            summary.reachedLevel = raw.ReachedLevel;
            summary.exp = raw.Exp;
            summary.reachedMilestone = raw.ReachedMilestone;

            BattleStats stats = raw.Stats;
            BattleStatsDto target = summary.stats;
            target.breakerDamage = stats.BreakerDamage;
            target.breakerCriticalDamage = stats.BreakerCriticalDamage;
            target.breakerTicks = stats.BreakerTicks;
            target.electricAsteroidDamage = stats.ElectricAsteroidDamage;
            target.electricStarDamage = stats.ElectricStarDamage;
            target.laserDamage = stats.LaserDamage;
            target.supernovaDamage = stats.SupernovaDamage;
            target.goldenAsteroidGold = stats.GoldenAsteroidGold;
            target.collectedMoons = stats.CollectedMoons;
            target.collectedComets = stats.CollectedComets;
            target.addedSeconds = stats.AddedSeconds;
        }

        // 이 판에 실제로 적용된 수치(노드 반영). 판을 막 시작해 흐른 시간이 0일 때 부른다.
        private static void FillApplied(AppliedStatsDto applied, GameSession session)
        {
            World world = session.World;
            Hq hq = world.Hq;
            BreakerDefinition breaker = world.Breaker.Definition;

            applied.startLevel = hq.StartLevel;
            applied.startExp = hq.Growth.ExpToReach(hq.StartLevel) ?? 0;
            applied.goalLevel = hq.GoalLevel;
            applied.goalExp = hq.GoalLevel == HqGrowthDefinition.NoGoal ? 0 : hq.Growth.ExpToReach(hq.GoalLevel) ?? 0;

            // 아직 흐르지 않았으므로 남은 시간이 곧 이 판의 제한 시간이다.
            applied.timeLimitSeconds = session.Remaining;
            applied.growthTimeSeconds = hq.GrowthTime;

            applied.breakerDamage = breaker.Damage;
            applied.breakerInterval = breaker.Interval;
            applied.breakerRadius = breaker.Radius;
            applied.breakerCritChance = breaker.CritChance;
            applied.breakerCritDamage = breaker.CritDamage;

            foreach (EnemyDefinition kind in world.Stats.Kinds)
            {
                // 픽업(혜성)은 성질이 언제나 붙으므로 확률을 적지 않는다(계약 참고).
                if (kind.IsPickup)
                    continue;

                EnemyComposition composition = world.Stats.CompositionOf(kind);
                string enemyId = ContractIds.Of(kind.Type.ToString());

                for (int i = 0; i < composition.Traits.Count; i++)
                {
                    EnemyTraitDefinition trait = composition.Traits[i];
                    TraitChanceDto chance = new()
                    {
                        enemyId = enemyId,
                        traitId = ContractIds.Of(trait.Type.ToString()),
                        chance = composition.TraitChances[i],
                    };
                    applied.traitChances.Add(chance);

                    if (kind.Type == EnemyType.Asteroid && trait.Effect is GoldenDefinition golden)
                        applied.goldenAsteroidMultiplier = golden.Multiplier;
                }
            }
        }

        // ISO 8601(UTC). 예: 2026-10-05T03:00:00.0000000Z
        private static string Timestamp(DateTime utc) => utc.ToString("o", CultureInfo.InvariantCulture);
    }
}
