using System;
using System.Collections.Generic;
using System.Globalization;

namespace BlackHole.Core
{
    // 진행 저장(ProgressSaveData) ↔ 진행 상태(PlayerState).
    //
    // 불러올 때 한 번 검사한다. 저장 형식이 틀리면 깨진 저장이다:
    // 형식 버전, 음수 Gold·성장도, 빈 NodeId·중복 NodeId, 1보다 작은 Rank.
    // 지금 콘텐츠와 맞지 않는 값은 깨진 것이 아니다. 게임에서는 맞춰 쓰고, 다음 저장 때 원래 값을 되돌려 적는다:
    // - 노드 콘텐츠에 없는 노드는 게임에서 뺀다.
    // - Rank 수(MaxRank)를 넘는 Rank는 MaxRank까지만 쓴다.
    // - 마지막 성장도를 넘는 성장도는 마지막 성장도로 쓴다.
    // 진행은 줄지 않으므로, 되돌려 적을 때는 원래 값과 지금 값 중 큰 쪽을 적는다.
    public static class ProgressSave
    {
        public static ProgressLoadResult Load(ProgressSaveData data, NodeContent nodes, HqGrowthDefinition growth)
        {
            var diagnostics = new List<ContentDiagnostic>();
            var adjustments = new List<ContentDiagnostic>();

            if (data.FormatVersion != ProgressSaveData.CurrentFormatVersion)
            {
                diagnostics.Add(new ContentDiagnostic("FormatVersion",
                    $"지원하지 않는 형식 버전이다. 받은 값: {data.FormatVersion}, 지금 형식: {ProgressSaveData.CurrentFormatVersion}."));
                return new ProgressLoadResult(null, diagnostics, adjustments);
            }

            if (data.Gold < 0)
                diagnostics.Add(new ContentDiagnostic("Gold", $"0 이상이어야 한다. 받은 값: {data.Gold}."));

            int stage = data.GrowthStage;

            if (stage < HqGrowthDefinition.StartStage)
                diagnostics.Add(new ContentDiagnostic("GrowthStage", $"{HqGrowthDefinition.StartStage} 이상이어야 한다. 받은 값: {stage}."));
            else if (stage > growth.MaxStage)
            {
                adjustments.Add(new ContentDiagnostic("GrowthStage", $"마지막 성장도보다 크다({stage} > {growth.MaxStage}). 게임에서는 {growth.MaxStage}로 쓴다."));
                stage = growth.MaxStage;
            }

            var ranks = new List<(string NodeId, int Rank)>(data.Nodes.Count);
            var firstAt = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int i = 0; i < data.Nodes.Count; i++)
            {
                NodeRankSaveData saved = data.Nodes[i];
                string at = $"Nodes[{i}]";

                if (string.IsNullOrEmpty(saved.NodeId))
                {
                    diagnostics.Add(new ContentDiagnostic(at, "NodeId가 비어 있다."));
                    continue;
                }

                if (firstAt.TryGetValue(saved.NodeId, out int first))
                {
                    diagnostics.Add(new ContentDiagnostic(at, $"'{saved.NodeId}'가 Nodes[{first}]에도 있다."));
                    continue;
                }

                firstAt.Add(saved.NodeId, i);

                if (saved.Rank < 1)
                {
                    diagnostics.Add(new ContentDiagnostic(at, $"'{saved.NodeId}'의 Rank는 1 이상이어야 한다. 받은 값: {saved.Rank}."));
                    continue;
                }

                if (!nodes.TryGetNode(saved.NodeId, out NodeDefinition node))
                {
                    adjustments.Add(new ContentDiagnostic(at, $"노드 콘텐츠에 없는 노드다: '{saved.NodeId}'. 게임에서는 뺀다."));
                    continue;
                }

                int rank = saved.Rank;

                if (rank > node.MaxRank)
                {
                    adjustments.Add(new ContentDiagnostic(at, $"'{saved.NodeId}'의 Rank가 Rank 수보다 크다({rank} > {node.MaxRank}). 게임에서는 {node.MaxRank}까지만 쓴다."));
                    rank = node.MaxRank;
                }

                ranks.Add((saved.NodeId, rank));
            }

            SavedProgress progress = diagnostics.Count == 0 ? new SavedProgress(data, stage, ranks) : null;
            return new ProgressLoadResult(progress, diagnostics, adjustments);
        }

        // 저장으로 진행 상태를 덮어쓴다. 지금 값은 모두 버린다.
        public static void Restore(PlayerState state, SavedProgress progress) =>
            state.Restore(progress.Gold, progress.GrowthStage, progress.Ranks);

        // 지금 진행 상태를 저장 형식으로. basis는 불러온 저장이다 — 게임에 쓰지 않은 원래 값을 되돌려 적는다. 새 게임이면 null.
        public static ProgressSaveData Capture(PlayerState state, SavedProgress basis, DateTime savedAtUtc)
        {
            var data = new ProgressSaveData
            {
                FormatVersion = ProgressSaveData.CurrentFormatVersion,
                Gold = state.Gold,
                GrowthStage = state.GrowthStage,
                SavedAtUtc = savedAtUtc.ToString("o", CultureInfo.InvariantCulture),
            };

            var written = new HashSet<string>(StringComparer.Ordinal);

            if (basis != null)
            {
                data.GrowthStage = Math.Max(data.GrowthStage, basis.Data.GrowthStage);

                foreach (NodeRankSaveData saved in basis.Data.Nodes)
                {
                    data.Nodes.Add(new NodeRankSaveData { NodeId = saved.NodeId, Rank = Math.Max(saved.Rank, state.RankOf(saved.NodeId)) });
                    written.Add(saved.NodeId);
                }
            }

            foreach (string nodeId in state.OwnedNodes)
            {
                if (written.Add(nodeId))
                    data.Nodes.Add(new NodeRankSaveData { NodeId = nodeId, Rank = state.RankOf(nodeId) });
            }

            return data;
        }
    }
}
