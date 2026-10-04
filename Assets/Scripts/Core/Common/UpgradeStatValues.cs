using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 업그레이드 수치의 값: 수치마다 기본값 + 산 효과 값의 합을 하한·상한으로 자른 값.
    // 값은 시트 단위 그대로다(Percent면 125 = 125%). 비율로 바꾸는 일은 수치를 읽는 쪽이 한다.
    // 합성은 덧셈뿐이다(UpgradeStatDefinition). 판마다 한 번 만들고 바꾸지 않는다.
    public sealed class UpgradeStatValues
    {
        private readonly Dictionary<string, UpgradeStatDefinition> _stats = new(StringComparer.Ordinal);
        private readonly Dictionary<string, double> _sums = new(StringComparer.Ordinal);

        // 수치 정의 순서(UpgradeStats 시트 순서).
        public IReadOnlyList<UpgradeStatDefinition> Stats { get; }

        // sums: StatId마다 산 효과 값의 합. 없는 StatId는 합이 0이다. 정의에 없는 StatId가 있으면 예외다.
        public UpgradeStatValues(IReadOnlyList<UpgradeStatDefinition> stats, IReadOnlyDictionary<string, double> sums)
        {
            if (stats == null)
                throw new ArgumentNullException(nameof(stats));

            var copy = new UpgradeStatDefinition[stats.Count];

            for (int i = 0; i < copy.Length; i++)
            {
                UpgradeStatDefinition stat = stats[i] ?? throw new ArgumentException($"수치 정의 {i}가 null이다.", nameof(stats));

                if (_stats.ContainsKey(stat.StatId))
                    throw new ArgumentException($"StatId '{stat.StatId}'가 두 번 있다.", nameof(stats));

                _stats.Add(stat.StatId, stat);
                copy[i] = stat;
            }

            if (sums != null)
            {
                foreach (KeyValuePair<string, double> sum in sums)
                {
                    if (!_stats.ContainsKey(sum.Key))
                        throw new ArgumentException($"수치 정의에 없는 StatId다: '{sum.Key}'.", nameof(sums));

                    if (double.IsNaN(sum.Value) || double.IsInfinity(sum.Value))
                        throw new ArgumentOutOfRangeException(nameof(sums), $"'{sum.Key}'의 합이 유한하지 않다.");

                    _sums.Add(sum.Key, sum.Value);
                }
            }

            Stats = Array.AsReadOnly(copy);
        }

        public bool Has(string statId) => statId != null && _stats.ContainsKey(statId);

        public bool TryGetDefinition(string statId, out UpgradeStatDefinition stat)
        {
            stat = null;
            return statId != null && _stats.TryGetValue(statId, out stat);
        }

        // 산 효과 값의 합(자르기 전).
        public float SumOf(string statId) => (float)Sum(Definition(statId).StatId);

        // 기본값 + 합을 하한·상한으로 자른 값.
        public float ValueOf(string statId)
        {
            UpgradeStatDefinition stat = Definition(statId);
            double value = stat.DefaultValue + Sum(stat.StatId);
            return (float)Math.Min(stat.Max, Math.Max(stat.Min, value));
        }

        private double Sum(string statId) => _sums.TryGetValue(statId, out double sum) ? sum : 0;

        private UpgradeStatDefinition Definition(string statId)
        {
            if (statId == null)
                throw new ArgumentNullException(nameof(statId));

            if (!_stats.TryGetValue(statId, out UpgradeStatDefinition stat))
                throw new ArgumentException($"수치 정의에 없는 StatId다: '{statId}'.", nameof(statId));

            return stat;
        }
    }
}
