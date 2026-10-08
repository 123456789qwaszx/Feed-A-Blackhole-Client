using System.Collections.Generic;
using System.Text;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 게임 콘텐츠 세트(GameContentSetup) → 게임 정의(GameContent·NodeTree)와 업그레이드 화면에 그릴 노드. Unity 에셋과 Core 사이의 경계다.
    // 빠진 연결은 여기서 한 번 보고, 규칙은 Core 로더들이 본다. 차례로 불러 단계마다 진단을 콘솔에 남긴다.
    // 오류가 하나라도 있으면 null이다(부분 통과 금지).
    public static class GameContentLoader
    {
        public static LoadedContent Load(GameContentSetup setup)
        {
            if (setup == null)
            {
                Debug.LogError("[콘텐츠] GameBootstrap에 게임 콘텐츠 세트(GameContentSetup)를 연결해야 한다.");
                return null;
            }

            IReadOnlyList<string> missing = setup.MissingReferences();

            if (missing.Count > 0)
            {
                Debug.LogError($"[콘텐츠] 게임 콘텐츠 세트에 연결하지 않은 칸이 있다: {string.Join(", ", missing)}.", setup);
                return null;
            }

            ContentLoadResult content = ContentLoader.Load(setup.ToData());
            LogErrors("콘텐츠", content.Diagnostics, setup);

            if (!content.Succeeded)
                return null;

            NodeCatalog nodes = setup.Nodes;

            if (nodes.Content == null)
            {
                Debug.LogError("[노드 콘텐츠] 노드 목록(NodeCatalog)에 노드 콘텐츠(NodeContentSource)를 연결해야 한다.", nodes);
                return null;
            }

            NodeContentLoadResult nodeContent = nodes.Content.Load();
            LogErrors("노드 콘텐츠", nodeContent.Diagnostics, nodes.Content);

            if (!nodeContent.Succeeded)
                return null;

            NodeTreeData layout = nodes.ToData();
            NodeTreeLoadResult tree = NodeTreeLoader.Load(layout, nodeContent.Content);
            LogErrors("노드 트리", tree.Diagnostics, nodes);

            if (!tree.Succeeded)
                return null;

            if (tree.Unplaced.Count > 0)
                Debug.LogWarning($"[노드 트리] 배치하지 않은 노드 {tree.Unplaced.Count}개는 트리에서 빠졌다(살 수 없다). 노드 도구(BlackHole > Node Tree)에서 놓는다.", nodes);

            // 노드를 모두 산 경우에도 판을 조립할 수 있어야 한다. 두 데이터는 따로 불러오므로 여기서 함께 본다.
            IReadOnlyList<ContentDiagnostic> fits = UpgradeContentCheck.Check(content.Content, tree.Tree);
            LogErrors("노드 트리 × 콘텐츠", fits, nodes);

            if (fits.Count > 0)
                return null;

            return new LoadedContent(content.Content, tree.Tree, NodeItemsOf(layout, tree.Tree));
        }

        // 업그레이드 화면에 그릴 노드(배치 순서). 칸은 화면 배치용이라 노드 트리가 아니라 배치 데이터에서 읽는다.
        // 노드 트리 로드를 통과한 배치이므로 모든 노드가 ID가 유일하고 트리에 있다.
        private static IReadOnlyList<NodeItem> NodeItemsOf(NodeTreeData layout, NodeTree tree)
        {
            var items = new List<NodeItem>(layout.Nodes.Count);

            foreach (NodeData placed in layout.Nodes)
            {
                tree.TryGet(placed.Id, out NodeDefinition node);
                NodeRankDefinition first = node.RankAt(1);
                string stat = tree.Content.StatOf(first.Effects[0].Stat).StatId;
                items.Add(new NodeItem(node.Id, placed.X, placed.Y, first.Cost, stat, node.MaxRank));
            }

            return items.AsReadOnly();
        }

        // 단계마다 로그 하나: 머리 줄 아래에 진단을 한 줄씩.
        private static void LogErrors(string stage, IReadOnlyList<ContentDiagnostic> diagnostics, Object context)
        {
            if (diagnostics.Count == 0)
                return;

            var text = new StringBuilder($"[{stage}] 오류 {diagnostics.Count}개");

            foreach (ContentDiagnostic diagnostic in diagnostics)
                text.Append("\n  ").Append(diagnostic);

            Debug.LogError(text.ToString(), context);
        }
    }
}
