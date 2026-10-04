using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 노드의 Rank 하나: 이 Rank를 살 때의 비용과, 사면 받는 효과. Rank는 1부터 센다.
    public sealed class NodeRankDefinition
    {
        public int Rank { get; }
        // 원작의 비용은 1,000조(1e15)까지 오르므로 long이다.
        public long Cost { get; }
        public IReadOnlyList<NodeEffect> Effects { get; }

        internal NodeRankDefinition(int rank, long cost, IReadOnlyList<NodeEffect> effects)
        {
            if (rank < 1)
                throw new ArgumentOutOfRangeException(nameof(rank), "Rank는 1부터다.");

            if (cost <= 0)
                throw new ArgumentOutOfRangeException(nameof(cost), $"비용은 0보다 커야 한다. 받은 값: {cost}.");

            if (effects == null || effects.Count == 0)
                throw new ArgumentException("효과가 하나 이상 필요하다.", nameof(effects));

            var copy = new NodeEffect[effects.Count];

            for (int i = 0; i < copy.Length; i++)
                copy[i] = effects[i] ?? throw new ArgumentException($"효과 {i}가 null이다.", nameof(effects));

            Rank = rank;
            Cost = cost;
            Effects = Array.AsReadOnly(copy);
        }
    }
}
