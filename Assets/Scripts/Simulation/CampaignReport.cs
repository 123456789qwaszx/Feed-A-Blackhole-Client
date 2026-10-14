using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BlackHole.Core;

namespace BlackHole.Simulation
{
    // 전체 루프 결과를 파일로 남긴다(UTF-8, 쉼표 구분, 숫자는 문화권과 무관한 형식).
    // - battles.csv: 판마다 한 줄(판 결과 + 그 뒤 구매 요약).
    // - purchases.csv: 산 Rank마다 한 줄(판 번호, 순서, 노드, 값).
    // - summary.md: 사람이 읽는 요약(결과, 이정표까지 걸린 판·시간, 성장도별 판 길이와 Gold).
    public static class CampaignReport
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static readonly EnemyType[] KillColumns = { EnemyType.Asteroid, EnemyType.Planet, EnemyType.Star, EnemyType.Galaxy, EnemyType.Comet };

        public static void Write(CampaignResult result, string directory)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("폴더가 필요하다.", nameof(directory));

            Directory.CreateDirectory(directory);
            var utf8 = new UTF8Encoding(false);
            File.WriteAllText(Path.Combine(directory, "battles.csv"), BattlesCsv(result), utf8);
            File.WriteAllText(Path.Combine(directory, "purchases.csv"), PurchasesCsv(result), utf8);
            File.WriteAllText(Path.Combine(directory, "summary.md"), Summary(result), utf8);
        }

        // 여러 시드로 돈 결과를 한데 모은다: runs.csv(실행마다 한 줄), runs.md(이정표까지 판 수·판 시간의 최소·중앙·최대).
        public static void WriteRuns(IReadOnlyList<CampaignResult> results, string directory)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("폴더가 필요하다.", nameof(directory));

            Directory.CreateDirectory(directory);
            var utf8 = new UTF8Encoding(false);
            File.WriteAllText(Path.Combine(directory, "runs.csv"), RunsCsv(results), utf8);
            File.WriteAllText(Path.Combine(directory, "runs.md"), RunsSummary(results), utf8);
        }

        public static string RunsCsv(IReadOnlyList<CampaignResult> results)
        {
            int stages = results.Count == 0 ? 0 : results.Max(r => r.Stages.Count == 0 ? 0 : r.Stages.Max(s => s.Stage));
            var text = new StringBuilder("seed,bot,end,battles,seconds,owned_ranks,total_ranks,final_gold");

            for (int stage = 1; stage <= stages; stage++)
                text.Append($",stage{stage}_battle,stage{stage}_seconds");

            text.Append('\n');

            foreach (CampaignResult result in results)
            {
                Append(text, result.Settings.Seed, result.Bot, result.End, result.Battles.Count, F(result.TotalSeconds), result.OwnedRanks, result.TotalRanks, result.FinalGold);

                for (int stage = 1; stage <= stages; stage++)
                {
                    StageReached? reached = FindStage(result, stage);
                    text.Append(',').Append(reached.HasValue ? reached.Value.Battle.ToString(Invariant) : "")
                        .Append(',').Append(reached.HasValue ? F(reached.Value.ElapsedSeconds) : "");
                }

                text.Append('\n');
            }

            return text.ToString();
        }

        public static string RunsSummary(IReadOnlyList<CampaignResult> results)
        {
            var text = new StringBuilder("# 전체 루프 시뮬레이션 — 여러 시드\n\n");
            int cleared = results.Count(r => r.End == CampaignEnd.Cleared);
            text.Append($"- 실행 {results.Count}번, 클리어 {cleared}번, 봇 {results.FirstOrDefault()?.Bot}\n\n");
            text.Append("| 지점 | 판 수 최소 / 중앙 / 최대 | 판 시간 합 최소 / 중앙 / 최대 |\n|---|---|---|\n");

            int stages = results.Count == 0 ? 0 : results.Max(r => r.Stages.Count == 0 ? 0 : r.Stages.Max(s => s.Stage));

            for (int stage = 1; stage <= stages; stage++)
            {
                List<StageReached> reached = results.Select(r => FindStage(r, stage)).Where(s => s.HasValue).Select(s => s.Value).ToList();

                if (reached.Count == 0)
                    continue;

                List<int> battles = reached.Select(s => s.Battle).OrderBy(x => x).ToList();
                List<double> seconds = reached.Select(s => s.ElapsedSeconds).OrderBy(x => x).ToList();
                text.Append($"| 성장도 {stage} (Level {reached[0].Level}, {reached.Count}번) | {battles[0]} / {battles[battles.Count / 2]} / {battles[battles.Count - 1]} |")
                    .Append($" {Clock(seconds[0])} / {Clock(seconds[seconds.Count / 2])} / {Clock(seconds[seconds.Count - 1])} |\n");
            }

            List<int> ranks = results.Select(r => r.OwnedRanks).OrderBy(x => x).ToList();

            if (ranks.Count > 0)
                text.Append($"\n끝났을 때 노드 Rank: 최소 {ranks[0]} / 중앙 {ranks[ranks.Count / 2]} / 최대 {ranks[ranks.Count - 1]} (전체 {results[0].TotalRanks})\n");

            return text.ToString();
        }

        private static StageReached? FindStage(CampaignResult result, int stage)
        {
            foreach (StageReached reached in result.Stages)
            {
                if (reached.Stage == stage)
                    return reached;
            }

            return null;
        }

        public static string BattlesCsv(CampaignResult result)
        {
            var text = new StringBuilder();
            text.Append("battle,seed,bot,stage,next_stage,start_level,goal_level,reached_level,milestone,capped,seconds,elapsed_seconds,")
                .Append("earned_gold,settled_gold,gold_before,gold_after_settle,bought,spent,gold_after_buy,owned_ranks,next_cheapest_cost,")
                .Append("kills_total,kills_asteroid,kills_planet,kills_star,kills_galaxy,kills_comet,")
                .Append("breaker_damage,laser_damage,supernova_damage,electric_asteroid_damage,electric_star_damage,breaker_ticks,moons,comets,added_seconds,exp\n");

            foreach (CampaignBattle entry in result.Battles)
            {
                BattleRecord b = entry.Battle;
                Append(text, entry.Index, b.Seed, b.Bot, b.Stage, b.NextStage, b.StartLevel, b.GoalLevel, b.ReachedLevel, b.ReachedMilestone ? 1 : 0,
                    b.Capped ? 1 : 0, F(b.PlayedSeconds), F(entry.ElapsedSeconds),
                    b.EarnedGold, b.SettledGold, b.GoldBefore, b.GoldAfter, entry.Purchases.Count, entry.Spent, entry.GoldAfterPurchases, entry.OwnedRanks,
                    entry.NextCheapestCost.HasValue ? entry.NextCheapestCost.Value.ToString(Invariant) : "",
                    b.TotalKills);

                foreach (EnemyType type in KillColumns)
                    text.Append(',').Append(b.KillsOf(type).ToString(Invariant));

                text.Append(',').Append(F(b.BreakerDamage)).Append(',').Append(F(b.LaserDamage)).Append(',').Append(F(b.SupernovaDamage))
                    .Append(',').Append(F(b.ElectricAsteroidDamage)).Append(',').Append(F(b.ElectricStarDamage))
                    .Append(',').Append(b.BreakerTicks.ToString(Invariant)).Append(',').Append(b.CollectedMoons.ToString(Invariant))
                    .Append(',').Append(b.CollectedComets.ToString(Invariant)).Append(',').Append(F(b.AddedSeconds)).Append(',').Append(b.Exp.ToString(Invariant)).Append('\n');
            }

            return text.ToString();
        }

        public static string PurchasesCsv(CampaignResult result)
        {
            var text = new StringBuilder("battle,order,node_id,rank,stat,cost,gold_after,stage\n");

            foreach (CampaignBattle entry in result.Battles)
            {
                for (int i = 0; i < entry.Purchases.Count; i++)
                {
                    NodePurchaseRecord purchase = entry.Purchases[i];
                    Append(text, entry.Index, i + 1, purchase.NodeId, purchase.Rank, purchase.StatId, purchase.Cost, purchase.GoldAfter, entry.Battle.NextStage);
                    text.Append('\n');
                }
            }

            return text.ToString();
        }

        public static string Summary(CampaignResult result)
        {
            var text = new StringBuilder();
            string end = result.End switch
            {
                CampaignEnd.Cleared => "클리어(마지막 이정표 도달)",
                CampaignEnd.Stalled => $"막힘({result.Settings.StallBattles}판 연속 구매·이정표 없음)",
                _ => $"판 상한({result.Settings.MaxBattles}판)",
            };

            text.Append("# 전체 루프 시뮬레이션\n\n");
            text.Append($"- 결과: {end}\n");
            text.Append($"- 봇: {result.Bot}, 시드 {result.Settings.Seed}, 프레임 {1 / result.Settings.FrameSeconds:0}fps\n");
            text.Append($"- 판 {result.Battles.Count}개, 판 시간 합 {Clock(result.TotalSeconds)} (판 밖의 시간 제외)\n");
            text.Append($"- 노드 Rank {result.OwnedRanks}/{result.TotalRanks}, 남은 Gold {result.FinalGold:N0}, 성장도 {result.FinalStage}\n");

            int capped = result.Battles.Count(b => b.Battle.Capped);

            if (capped > 0)
                text.Append($"- 끝나지 않아 {result.Settings.MaxBattleSeconds:0}초에서 끊은 판 {capped}개(시간 추가가 흐르는 시간보다 빨랐다)\n");

            text.Append('\n');

            text.Append("## 이정표\n\n| 성장도 | 이정표 Level | 닿은 판 | 판 시간 합 |\n|---:|---:|---:|---:|\n");

            foreach (StageReached stage in result.Stages)
                text.Append($"| {stage.Stage} | {stage.Level} | {stage.Battle} | {Clock(stage.ElapsedSeconds)} |\n");

            text.Append("\n## 성장도별 판\n\n| 성장도 | 판 수 | 판 길이 평균 | 판당 번 Gold 중앙값 | 산 Rank | 성장도 끝의 Rank |\n|---:|---:|---:|---:|---:|---:|\n");

            foreach (IGrouping<int, CampaignBattle> group in result.Battles.GroupBy(b => b.Battle.Stage))
            {
                List<CampaignBattle> battles = group.ToList();
                double length = battles.Average(b => b.Battle.PlayedSeconds);
                List<long> earned = battles.Select(b => b.Battle.EarnedGold).OrderBy(g => g).ToList();
                int bought = battles.Sum(b => b.Purchases.Count);
                text.Append($"| {group.Key} | {battles.Count} | {length:0.0}초 | {earned[earned.Count / 2]:N0} | {bought} | {battles[battles.Count - 1].OwnedRanks} |\n");
            }

            text.Append("\n## 처음 10판\n\n| 판 | Level | 판 길이 | 번 Gold | 산 Rank | 남은 Gold |\n|---:|---|---:|---:|---:|---:|\n");

            foreach (CampaignBattle entry in result.Battles.Take(10))
            {
                BattleRecord b = entry.Battle;
                text.Append($"| {entry.Index} | {b.StartLevel} → {b.ReachedLevel} | {b.PlayedSeconds:0.0}초 | {b.EarnedGold:N0} | {entry.Purchases.Count} | {entry.GoldAfterPurchases:N0} |\n");
            }

            return text.ToString();
        }

        private static string Clock(double seconds)
        {
            var span = TimeSpan.FromSeconds(seconds);
            return span.TotalHours >= 1 ? $"{(int)span.TotalHours}시간 {span.Minutes}분 {span.Seconds}초" : $"{span.Minutes}분 {span.Seconds}초";
        }

        private static string F(double value) => value.ToString("0.###", Invariant);

        private static void Append(StringBuilder text, params object[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0)
                    text.Append(',');

                text.Append(Cell(values[i]));
            }
        }

        private static string Cell(object value)
        {
            string cell = value is IFormattable formattable ? formattable.ToString(null, Invariant) : value?.ToString() ?? "";
            return cell.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? "\"" + cell.Replace("\"", "\"\"") + "\"" : cell;
        }
    }
}
