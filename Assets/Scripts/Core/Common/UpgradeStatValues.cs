using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 수치마다 기본값 + 산 효과 값의 합을 Min·Max로 자른 값. 시트 단위 그대로다(Percent면 125 = 125%).
    public sealed class UpgradeStatValues
    {
        private readonly Dictionary<string, UpgradeStatDefinition> _stats = new(StringComparer.Ordinal);
        private readonly IReadOnlyDictionary<string, double> _sums;

        public IReadOnlyList<UpgradeStatDefinition> Stats { get; }

        public UpgradeStatValues(IReadOnlyList<UpgradeStatDefinition> stats, IReadOnlyDictionary<string, double> sums)
        {
            Stats = stats ?? throw new ArgumentNullException(nameof(stats));
            _sums = sums ?? throw new ArgumentNullException(nameof(sums));

            foreach (UpgradeStatDefinition stat in stats)
                _stats.Add(stat.StatId, stat);
        }

        public bool Has(string statId) => _stats.ContainsKey(statId);

        public UpgradeStatDefinition DefinitionOf(string statId) => _stats[statId];

        // 산 효과 값의 합(자르기 전).
        public float SumOf(string statId) => (float)Sum(statId);

        public float ValueOf(string statId)
        {
            UpgradeStatDefinition stat = _stats[statId];
            return (float)Math.Min(stat.Max, Math.Max(stat.Min, stat.DefaultValue + Sum(statId)));
        }

        private double Sum(string statId) => _sums.TryGetValue(statId, out double sum) ? sum : 0;
    }
}
