using System;
using System.Collections.Generic;
using BlackHole.Core;

namespace BlackHole.Simulation
{
    // 게임 전체 루프를 처음부터 클리어까지 돈다: 빈 진행 상태 → [판(봇 조준) → 결산 → 가장 싼 노드부터 살 수 있는 만큼 구매] 반복.
    // 클리어 = 마지막 이정표에 닿음(성장도가 HqGrowthDefinition.MaxStage). 판 상한이나 막힘(오래 아무것도 못 사고 성장도도 그대로)에서도 멈춘다.
    // 업그레이드 화면에서 보내는 시간, 판 시작 연출 등 판 밖의 시간은 세지 않는다(판 시간만 더한다).
    public static class CampaignRunner
    {
        public static CampaignResult Run(GameContent content, NodeTree tree, CampaignSettings settings, Action<CampaignBattle> onBattle = null)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (tree == null) throw new ArgumentNullException(nameof(tree));

            settings ??= new CampaignSettings();
            settings.Validate();

            var progress = new ProgressState();
            var result = new CampaignResult(settings, TotalRanksOf(tree));
            int maxStage = content.Growth.MaxStage;
            int stalled = 0;
            double clock = 0;

            for (int index = 1; index <= settings.MaxBattles; index++)
            {
                IAimBot bot = settings.CreateBot();
                result.Bot ??= bot.Name;
                int seed = unchecked(settings.Seed * 100003 + index);
                UpgradeStatValues upgrades = NodePurchase.StatsFor(progress, tree);

                BattleRecord battle = BattleRunner.Run(content, progress, upgrades, bot, seed, settings.FrameSeconds, settings.MaxBattleSeconds);
                List<NodePurchaseRecord> purchases = CheapestFirstBuyer.BuyAll(progress, tree);
                clock += battle.PlayedSeconds;

                long spent = 0;
                foreach (NodePurchaseRecord purchase in purchases)
                    spent += purchase.Cost;

                var entry = new CampaignBattle
                {
                    Index = index,
                    Battle = battle,
                    Purchases = purchases,
                    Spent = spent,
                    GoldAfterPurchases = progress.Gold,
                    OwnedRanks = OwnedRanksOf(progress, tree),
                    ElapsedSeconds = clock,
                    NextCheapestCost = CheapestFirstBuyer.CheapestRevealedCost(progress, tree),
                };

                result.Battles.Add(entry);

                if (battle.ReachedMilestone)
                    result.Stages.Add(new StageReached(battle.NextStage, battle.GoalLevel, index, clock));

                onBattle?.Invoke(entry);

                if (progress.GrowthStage >= maxStage)
                {
                    result.End = CampaignEnd.Cleared;
                    break;
                }

                stalled = purchases.Count == 0 && !battle.ReachedMilestone ? stalled + 1 : 0;

                if (stalled >= settings.StallBattles)
                {
                    result.End = CampaignEnd.Stalled;
                    break;
                }

                if (index == settings.MaxBattles)
                    result.End = CampaignEnd.MaxBattles;
            }

            result.FinalGold = progress.Gold;
            result.FinalStage = progress.GrowthStage;
            result.OwnedRanks = OwnedRanksOf(progress, tree);
            result.TotalSeconds = clock;
            return result;
        }

        private static int TotalRanksOf(NodeTree tree)
        {
            int total = 0;

            foreach (NodeDefinition node in tree.Nodes)
                total += node.MaxRank;

            return total;
        }

        private static int OwnedRanksOf(ProgressState progress, NodeTree tree)
        {
            int owned = 0;

            foreach (NodeDefinition node in tree.Nodes)
                owned += progress.RankOf(node.Id);

            return owned;
        }
    }

    public sealed class CampaignSettings
    {
        // 판마다의 시드는 이 값과 판 번호로 정한다. 같은 설정·콘텐츠면 같은 결과가 나온다.
        public int Seed { get; set; } = 1;

        // 판 상한.
        public int MaxBattles { get; set; } = 5000;

        // 이만큼 연달아 아무것도 못 사고 이정표에도 닿지 못하면 막힌 것으로 보고 멈춘다.
        public int StallBattles { get; set; } = 300;

        // 판을 진행하는 한 프레임(초).
        public float FrameSeconds { get; set; } = BattleRunner.DefaultFrameSeconds;

        // 판 하나의 상한(판 시간, 초). 끝나지 않는 판을 여기서 끊는다(BattleRecord.Capped).
        public float MaxBattleSeconds { get; set; } = BattleRunner.SafetySeconds;

        // 판마다 새 봇을 만든다.
        public Func<IAimBot> CreateBot { get; set; } = () => new GreedyAimBot();

        public void Validate()
        {
            if (MaxBattles <= 0) throw new ArgumentOutOfRangeException(nameof(MaxBattles), "1 이상이어야 한다.");
            if (StallBattles <= 0) throw new ArgumentOutOfRangeException(nameof(StallBattles), "1 이상이어야 한다.");
            if (CreateBot == null) throw new ArgumentNullException(nameof(CreateBot));

            if (float.IsNaN(FrameSeconds) || FrameSeconds <= 0 || FrameSeconds > 1)
                throw new ArgumentOutOfRangeException(nameof(FrameSeconds), "0보다 크고 1 이하인 초가 필요하다.");

            if (float.IsNaN(MaxBattleSeconds) || MaxBattleSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(MaxBattleSeconds), "양의 초가 필요하다.");
        }
    }

    public enum CampaignEnd
    {
        Cleared,   // 마지막 이정표에 닿았다.
        MaxBattles, // 판 상한에 닿았다.
        Stalled,   // 오래 아무것도 못 샀다.
    }

    // 전체 루프의 판 하나: 판 결과 + 그 뒤의 구매.
    public sealed class CampaignBattle
    {
        public int Index { get; set; } // 1부터.
        public BattleRecord Battle { get; set; }
        public List<NodePurchaseRecord> Purchases { get; set; }
        public long Spent { get; set; }
        public long GoldAfterPurchases { get; set; }
        public int OwnedRanks { get; set; }
        // 처음부터 이 판까지 판 시간의 합(초).
        public double ElapsedSeconds { get; set; }
        // 구매 뒤 드러난 노드 중 다음으로 가장 싼 값(다 샀으면 null).
        public long? NextCheapestCost { get; set; }
    }

    // 이정표에 처음 닿은 판.
    public readonly struct StageReached
    {
        public int Stage { get; }        // 닿은 뒤의 성장도(1부터).
        public int Level { get; }        // 이정표 Level.
        public int Battle { get; }       // 판 번호.
        public double ElapsedSeconds { get; } // 그때까지 판 시간의 합.

        public StageReached(int stage, int level, int battle, double elapsedSeconds)
        {
            Stage = stage;
            Level = level;
            Battle = battle;
            ElapsedSeconds = elapsedSeconds;
        }
    }

    public sealed class CampaignResult
    {
        public CampaignResult(CampaignSettings settings, int totalRanks)
        {
            Settings = settings;
            TotalRanks = totalRanks;
        }

        public CampaignSettings Settings { get; }
        public string Bot { get; set; }
        public CampaignEnd End { get; set; } = CampaignEnd.MaxBattles;
        public List<CampaignBattle> Battles { get; } = new List<CampaignBattle>();
        public List<StageReached> Stages { get; } = new List<StageReached>();
        public int TotalRanks { get; }
        public int OwnedRanks { get; set; }
        public long FinalGold { get; set; }
        public int FinalStage { get; set; }
        public double TotalSeconds { get; set; }
    }
}
