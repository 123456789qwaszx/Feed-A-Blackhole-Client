using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 노드 구매:
    // 화면이 노드 ID로 요청. 한 번에 다음 Rank 하나를 산다(Rank 1부터 차례로).
    // 노드는 Rank 1을 사는 순간 "산 노드"가 되어 선으로 이어진 노드를 드러낸다(PlayerState.Owns).
    public static class NodePurchase
    {
        // 지금 사면 어떻게 되는지. 상태를 바꾸지 않음.
        public static PurchaseResult Check(PlayerState state, NodeTree tree, string nodeId)
        {
            Verify(state, tree);
            return tree.TryGet(nodeId, out NodeDefinition node) ? Check(state, tree, node) : PurchaseResult.UnknownNode;
        }

        public static PurchaseResult TryPurchase(PlayerState state, NodeTree tree, string nodeId)
        {
            Verify(state, tree);

            if (!tree.TryGet(nodeId, out NodeDefinition node))
                return PurchaseResult.UnknownNode;

            PurchaseResult result = Check(state, tree, node);

            if (result == PurchaseResult.Purchased)
                state.BuyRank(node.Id, NextRankOf(state, node).Cost);

            return result;
        }

        public static NodeState StateOf(PlayerState state, NodeTree tree, string nodeId)
        {
            switch (Check(state, tree, nodeId))
            {
                case PurchaseResult.UnknownNode: throw new ArgumentException($"트리에 없는 노드다: '{nodeId}'.", nameof(nodeId));
                case PurchaseResult.MaxRankReached: return NodeState.Owned;
                case PurchaseResult.Hidden: return NodeState.Hidden;
                case PurchaseResult.Purchased: return NodeState.Purchasable;
                default: return NodeState.Revealed;
            }
        }

        // 다음에 살 Rank의 비용. 마지막 Rank까지 샀거나 트리에 없는 노드면 false.
        public static bool TryGetNextCost(PlayerState state, NodeTree tree, string nodeId, out long cost)
        {
            Verify(state, tree);
            cost = 0;

            if (!tree.TryGet(nodeId, out NodeDefinition node))
                return false;

            NodeRankDefinition next = NextRankOf(state, node);

            if (next == null)
                return false;

            cost = next.Cost;
            return true;
        }

        // 산 Rank의 효과로 계산한 수치 값(시트 단위). 노드 트리와 업그레이드 수치가 만나는 자리는 여기 하나.
        public static UpgradeStatValues StatsFor(PlayerState state, NodeTree tree)
        {
            Verify(state, tree);
            return StatsFor(tree, node => Math.Min(state.RankOf(node.Id), node.MaxRank));
        }

        // [임시] 전투 쪽이 지금 읽는 옛 업그레이드 표. StatsFor의 값을 옛 수치 이름으로 옮긴다(NodeUpgradeBridge).
        public static UpgradeTable UpgradesFor(PlayerState state, NodeTree tree) =>
            NodeUpgradeBridge.ToUpgradeTable(StatsFor(state, tree));

        // 노드마다 ranksOf(node)까지 산 것으로 보고 수치 값을 계산한다. 로드 때의 검사(UpgradeContentCheck)는 모두 산 경우를 본다.
        internal static UpgradeStatValues StatsFor(NodeTree tree, Func<NodeDefinition, int> ranksOf)
        {
            var sums = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (NodeDefinition node in tree.Nodes)
            {
                int ranks = ranksOf(node);

                for (int rank = 1; rank <= ranks; rank++)
                {
                    foreach (NodeEffect effect in node.RankAt(rank).Effects)
                        sums[effect.StatId] = (sums.TryGetValue(effect.StatId, out double sum) ? sum : 0) + effect.Value;
                }
            }

            return new UpgradeStatValues(tree.Content.Stats, sums);
        }

        // 판정 순서: 마지막 Rank → 숨김 → Gold.
        private static PurchaseResult Check(PlayerState state, NodeTree tree, NodeDefinition node)
        {
            NodeRankDefinition next = NextRankOf(state, node);

            if (next == null)
                return PurchaseResult.MaxRankReached;

            if (!tree.Graph.IsRevealed(node.Id, state.Owns))
                return PurchaseResult.Hidden;

            if (state.Gold < next.Cost)
                return PurchaseResult.NotEnoughGold;

            return PurchaseResult.Purchased;
        }

        // 다음에 살 Rank. 마지막 Rank까지 샀으면 null.
        private static NodeRankDefinition NextRankOf(PlayerState state, NodeDefinition node)
        {
            int owned = state.RankOf(node.Id);
            return owned < node.MaxRank ? node.RankAt(owned + 1) : null;
        }

        private static void Verify(PlayerState state, NodeTree tree)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            if (tree == null)
                throw new ArgumentNullException(nameof(tree));
        }
    }
}
