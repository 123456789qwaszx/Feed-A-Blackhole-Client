using System;

namespace BlackHole.Core
{
    // 값은 시트에 적힌 그대로다: Percent는 25가 25%다. 비율(0.25)로 바꾸는 일은 수치를 읽는 쪽이 한다.
    public enum UpgradeStatUnit
    {
        Flat,
        Percent,
    }

    public enum UpgradeStatValueType
    {
        Float,
        Int,
    }

    public sealed class UpgradeStatDefinition
    {
        public UpgradeStat Stat { get; }
        // 시트에 적힌 이름(breaker.critChance). 표시와 노드 그림 키가 쓴다.
        public string StatId { get; }
        public UpgradeStatValueType ValueType { get; }
        public UpgradeStatUnit Unit { get; }
        public float DefaultValue { get; }
        // 시트 칸이 비어 있으면 각각 음·양의 무한대다.
        public float Min { get; }
        public float Max { get; }
        // [미정] 끈 수치를 어떻게 다룰지는 정하지 않았다.
        public bool Enabled { get; }

        public UpgradeStatDefinition(
            UpgradeStat stat,
            string statId,
            UpgradeStatValueType valueType,
            UpgradeStatUnit unit,
            float defaultValue,
            float min,
            float max,
            bool enabled)
        {
            Stat = stat;
            StatId = statId ?? throw new ArgumentNullException(nameof(statId));
            ValueType = valueType;
            Unit = unit;
            DefaultValue = defaultValue;
            Min = min;
            Max = max;
            Enabled = enabled;
        }
    }
}
