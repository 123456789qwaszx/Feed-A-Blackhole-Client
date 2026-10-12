using System;
using System.Collections.Generic;
using BlackHole.Core;

namespace BlackHole.Simulation
{
    // 노드 구매 정책: 지금 살 수 있는(드러났고 Gold가 되는) 노드 중 다음 Rank가 가장 싼 것부터, 살 수 없을 때까지 산다.
    // 값이 같으면 노드 ID 순서(ordinal)다 — 테스트 세팅의 자동 구매(ScenarioRunner)와 같은 규칙이다.
    // 산 노드가 이웃을 드러내므로 한 번 살 때마다 다시 고른다.
    public static class CheapestFirstBuyer
    {
        public static List<NodePurchaseRecord> BuyAll(ProgressState progress, NodeTree tree)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (tree == null) throw new ArgumentNullException(nameof(tree));

            var bought = new List<NodePurchaseRecord>();

            while (true)
            {
                NodeDefinition cheapest = null;
                long cheapestCost = long.MaxValue;

                foreach (NodeDefinition node in tree.Nodes)
                {
                    if (NodePurchase.Check(progress, tree, node.Id) != PurchaseResult.Purchased)
                        continue;

                    if (!NodePurchase.TryGetNextCost(progress, tree, node.Id, out long cost))
                        continue;

                    if (cheapest == null || cost < cheapestCost || (cost == cheapestCost && string.CompareOrdinal(node.Id, cheapest.Id) < 0))
                    {
                        cheapest = node;
                        cheapestCost = cost;
                    }
                }

                if (cheapest == null)
                    return bought;

                int rank = progress.RankOf(cheapest.Id) + 1;
                NodePurchase.TryPurchase(progress, tree, cheapest.Id);
                NodeRankDefinition definition = cheapest.RankAt(rank);
                string stat = definition.Effects.Count > 0 ? tree.Content.StatOf(definition.Effects[0].Stat).StatId : "";
                bought.Add(new NodePurchaseRecord(cheapest.Id, rank, stat, cheapestCost, progress.Gold));
            }
        }

        // 드러난 노드(살 수 있거나 Gold가 모자란 노드)의 다음 Rank 중 가장 싼 값. 드러난 노드를 다 샀으면 null.
        public static long? CheapestRevealedCost(ProgressState progress, NodeTree tree)
        {
            long? cheapest = null;

            foreach (NodeDefinition node in tree.Nodes)
            {
                PurchaseResult state = NodePurchase.Check(progress, tree, node.Id);

                if (state != PurchaseResult.Purchased && state != PurchaseResult.NotEnoughGold)
                    continue;

                if (NodePurchase.TryGetNextCost(progress, tree, node.Id, out long cost) && (cheapest == null || cost < cheapest))
                    cheapest = cost;
            }

            return cheapest;
        }
    }

    // 노드 Rank 하나를 산 기록.
    public readonly struct NodePurchaseRecord
    {
        public string NodeId { get; }
        public int Rank { get; }
        // 그 Rank가 올리는 첫 수치의 이름(UpgradeStats 시트의 StatId).
        public string StatId { get; }
        public long Cost { get; }
        public long GoldAfter { get; }

        public NodePurchaseRecord(string nodeId, int rank, string statId, long cost, long goldAfter)
        {
            NodeId = nodeId;
            Rank = rank;
            StatId = statId;
            Cost = cost;
            GoldAfter = goldAfter;
        }
    }
}
