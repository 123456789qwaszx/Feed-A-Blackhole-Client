using System;

namespace BlackHole.Core
{
    // 수치 값의 단위. 값은 시트에 적힌 그대로 둔다: Percent는 25가 25%다. 비율(0.25)로 바꾸는 일은 수치를 읽는 쪽이 한다.
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

    // 업그레이드 수치 하나의 정의(UpgradeStats 시트 한 행). 노드의 효과(NodeEffect)는 StatId로 이 수치를 올린다.
    // 수치 = 기본값 + 산 효과 값의 합, 하한·상한으로 자른다. 합성은 덧셈뿐이다(시트 Aggregation = Add).
    // 이 수치가 게임에서 무엇을 하는지는 수치를 읽는 시스템이 정한다. 정의는 이름·단위·범위만 안다.
    public sealed class UpgradeStatDefinition
    {
        public string StatId { get; }
        public UpgradeStatValueType ValueType { get; }
        public UpgradeStatUnit Unit { get; }
        // 노드를 하나도 사지 않았을 때의 값. 시트 단위 그대로다(Percent면 100 = 100%).
        public float DefaultValue { get; }
        // 하한·상한. 시트 칸이 비어 있으면 각각 음·양의 무한대다.
        public float Min { get; }
        public float Max { get; }
        // [미정] 끈 수치를 어떻게 다룰지(효과 무시, 노드 숨김 등)는 정하지 않았다. 값만 가진다.
        public bool Enabled { get; }

        public UpgradeStatDefinition(
            string statId,
            UpgradeStatValueType valueType,
            UpgradeStatUnit unit,
            float defaultValue,
            float min = float.NegativeInfinity,
            float max = float.PositiveInfinity,
            bool enabled = true)
        {
            if (string.IsNullOrWhiteSpace(statId))
                throw new ArgumentException("StatId가 비어 있다.", nameof(statId));

            if (!Enum.IsDefined(typeof(UpgradeStatValueType), valueType))
                throw new ArgumentOutOfRangeException(nameof(valueType));

            if (!Enum.IsDefined(typeof(UpgradeStatUnit), unit))
                throw new ArgumentOutOfRangeException(nameof(unit));

            if (float.IsNaN(defaultValue) || float.IsInfinity(defaultValue))
                throw new ArgumentOutOfRangeException(nameof(defaultValue), "기본값은 유한한 값이어야 한다.");

            if (float.IsNaN(min) || float.IsPositiveInfinity(min))
                throw new ArgumentOutOfRangeException(nameof(min), "하한은 유한한 값이거나 비어 있어야 한다.");

            if (float.IsNaN(max) || float.IsNegativeInfinity(max))
                throw new ArgumentOutOfRangeException(nameof(max), "상한은 유한한 값이거나 비어 있어야 한다.");

            if (min > max)
                throw new ArgumentOutOfRangeException(nameof(max), $"상한({max})이 하한({min})보다 작다.");

            if (defaultValue < min || defaultValue > max)
                throw new ArgumentOutOfRangeException(nameof(defaultValue), $"기본값({defaultValue})이 하한·상한 [{min}, {max}] 밖이다.");

            if (valueType == UpgradeStatValueType.Int && (!IsWhole(defaultValue) || !IsWholeOrInfinite(min) || !IsWholeOrInfinite(max)))
                throw new ArgumentException("정수 수치는 기본값·하한·상한이 정수여야 한다.", nameof(valueType));

            StatId = statId;
            ValueType = valueType;
            Unit = unit;
            DefaultValue = defaultValue;
            Min = min;
            Max = max;
            Enabled = enabled;
        }

        // 이 수치에 더할 수 있는 효과 값인가: 유한하고, 정수 수치면 정수다.
        public bool Accepts(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && (ValueType == UpgradeStatValueType.Float || IsWhole(value));

        public override string ToString() => $"{StatId} ({ValueType}, {Unit}, 기본 {DefaultValue})";

        private static bool IsWhole(float value) => !float.IsInfinity(value) && value == Math.Floor(value);

        private static bool IsWholeOrInfinite(float value) => float.IsInfinity(value) || value == Math.Floor(value);
    }
}
