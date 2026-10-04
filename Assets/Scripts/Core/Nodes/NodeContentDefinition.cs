using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 노드 하나의 콘텐츠: ID와 Rank 목록. 칸·선·시작 노드(배치)는 모른다 — 노드 도구의 것이다.
    // ID는 Nodes 시트가 정하는 식별자이자 진행 상태의 저장 키다. 모양({StatId}-{NN})에 뜻을 두지 않는다.
    // 반복 노드(원작의 Rank 10 노드)도 노드 하나다: Ranks[i].Rank == i + 1.
    // [임시 이름] 노드 Rank 구매로 넘어갈 때 지금의 NodeDefinition(Id, Price, Upgrades)을 대신한다.
    public sealed class NodeContentDefinition
    {
        public string Id { get; }
        // 기획 메모(Nodes 시트 Memo). 게임 규칙에 쓰지 않는다.
        public string Memo { get; }
        public IReadOnlyList<NodeRankDefinition> Ranks { get; }
        public int MaxRank => Ranks.Count;

        internal NodeContentDefinition(string id, string memo, IReadOnlyList<NodeRankDefinition> ranks)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("NodeId가 비어 있다.", nameof(id));

            if (ranks == null || ranks.Count == 0)
                throw new ArgumentException("Rank가 하나 이상 필요하다.", nameof(ranks));

            var copy = new NodeRankDefinition[ranks.Count];

            for (int i = 0; i < copy.Length; i++)
            {
                NodeRankDefinition rank = ranks[i] ?? throw new ArgumentException($"Rank {i + 1}이 null이다.", nameof(ranks));

                if (rank.Rank != i + 1)
                    throw new ArgumentException($"Rank는 1부터 빠짐없이 차례여야 한다. {i + 1}번째가 Rank {rank.Rank}다.", nameof(ranks));

                copy[i] = rank;
            }

            Id = id;
            Memo = memo ?? string.Empty;
            Ranks = Array.AsReadOnly(copy);
        }

        // rank번째 Rank(1부터).
        public NodeRankDefinition RankAt(int rank)
        {
            if (rank < 1 || rank > Ranks.Count)
                throw new ArgumentOutOfRangeException(nameof(rank), $"'{Id}'의 Rank는 1부터 {Ranks.Count}까지다. 받은 값: {rank}.");

            return Ranks[rank - 1];
        }
    }
}
