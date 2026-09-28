using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 노드가 파는 것: ID, 가격, 사면 받는 업그레이드. 노드는 한 번만 산다.
    // 선과 시작 노드는 그래프(NodeGraph)가 가진다. 업그레이드의 뜻은 모른다 — 산 노드의 업그레이드를 모아 UpgradeTable에 넘길 뿐이다.
    public sealed class NodeDefinition
    {
        public string Id { get; }
        // 원작 가격은 T(조) 단위까지 오르므로 Gold와 같은 long이다.
        public long Price { get; }
        public IReadOnlyList<Upgrade> Upgrades { get; }

        internal NodeDefinition(string id, long price, IReadOnlyList<Upgrade> upgrades)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("ID가 비어 있다.", nameof(id));

            if (price <= 0)
                throw new ArgumentOutOfRangeException(nameof(price), "양의 정수가 필요하다.");

            var copy = new Upgrade[upgrades?.Count ?? 0];

            for (int i = 0; i < copy.Length; i++)
                copy[i] = upgrades[i];

            Id = id;
            Price = price;
            Upgrades = Array.AsReadOnly(copy);
        }
    }
}
