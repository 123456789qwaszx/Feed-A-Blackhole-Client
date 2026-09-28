using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 노드 구매:
    // 화면이 노드 ID로 요청.
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
                state.Buy(node.Id, node.Price);

            return result;
        }

        public static NodeState StateOf(PlayerState state, NodeTree tree, string nodeId)
        {
            switch (Check(state, tree, nodeId))
            {
                case PurchaseResult.UnknownNode: throw new ArgumentException($"트리에 없는 노드다: '{nodeId}'.", nameof(nodeId));
                case PurchaseResult.AlreadyOwned: return NodeState.Owned;
                case PurchaseResult.Hidden: return NodeState.Hidden;
                case PurchaseResult.Purchased: return NodeState.Purchasable;
                default: return NodeState.Revealed;
            }
        }

        // 산 노드들의 업그레이드를 모은 표. 노드 트리와 업그레이드가 만나는 자리는 여기 하나.
        public static UpgradeTable UpgradesFor(PlayerState state, NodeTree tree)
        {
            Verify(state, tree);
            var upgrades = new List<Upgrade>();

            foreach (NodeDefinition node in tree.Nodes)
            {
                if (state.Owns(node.Id))
                    upgrades.AddRange(node.Upgrades);
            }

            return new UpgradeTable(upgrades);
        }

        // 판정 순서: 산 것 → 숨김 → Gold.
        private static PurchaseResult Check(PlayerState state, NodeTree tree, NodeDefinition node)
        {
            if (state.Owns(node.Id))
                return PurchaseResult.AlreadyOwned;

            if (!tree.Graph.IsRevealed(node.Id, state.Owns))
                return PurchaseResult.Hidden;

            if (state.Gold < node.Price)
                return PurchaseResult.NotEnoughGold;

            return PurchaseResult.Purchased;
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
