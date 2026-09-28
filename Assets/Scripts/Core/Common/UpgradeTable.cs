using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    public sealed class UpgradeTable
    {
        private readonly Dictionary<string, Sum> _sums = new Dictionary<string, Sum>(StringComparer.Ordinal);

        public UpgradeTable(IEnumerable<Upgrade> upgrades)
        {
            if (upgrades == null)
                throw new ArgumentNullException(nameof(upgrades));

            var sorted = new List<Upgrade>(upgrades);

            foreach (Upgrade upgrade in sorted)
            {
                if (upgrade.Stat == null)
                    throw new ArgumentException("생성자로 만들지 않은 업그레이드(default)가 있다.", nameof(upgrades));
            }

            // 소수의 합은 더하는 순서에 따라 끝자리가 달라진다. 받은 순서가 결과에 닿지 않도록 정렬한 뒤에 모은다.
            sorted.Sort(Compare);

            foreach (Upgrade upgrade in sorted)
            {
                if (!_sums.TryGetValue(upgrade.Stat, out Sum sum))
                    sum = Sum.None;

                _sums[upgrade.Stat] = sum.With(upgrade.Operation, upgrade.Value);
            }
        }

        public float Apply(string stat, float baseValue)
        {
            if (stat == null)
                throw new ArgumentNullException(nameof(stat));

            return _sums.TryGetValue(stat, out Sum sum) ? sum.Apply(baseValue) : baseValue;
        }

        private static int Compare(Upgrade a, Upgrade b)
        {
            int byStat = string.CompareOrdinal(a.Stat, b.Stat);

            if (byStat != 0)
                return byStat;

            int byOperation = a.Operation.CompareTo(b.Operation);
            return byOperation != 0 ? byOperation : a.Value.CompareTo(b.Value);
        }

        // 한 수치에 모인 업그레이드.
        private readonly struct Sum
        {
            public static readonly Sum None = new Sum(0, 0, 1);

            private readonly double _add;
            private readonly double _percent;
            private readonly double _multiply;

            private Sum(double add, double percent, double multiply)
            {
                _add = add;
                _percent = percent;
                _multiply = multiply;
            }

            public Sum With(UpgradeOperation operation, float value)
            {
                switch (operation)
                {
                    case UpgradeOperation.Add: return new Sum(_add + value, _percent, _multiply);
                    case UpgradeOperation.Percent: return new Sum(_add, _percent + value, _multiply);
                    default: return new Sum(_add, _percent, _multiply * value);
                }
            }

            public float Apply(float baseValue) => (float)((baseValue + _add) * (1 + _percent) * _multiply);
        }
    }
}
