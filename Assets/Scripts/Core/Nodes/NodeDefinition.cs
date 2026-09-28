using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    public sealed class NodeDefinition
    {
        public string Id { get; }

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
