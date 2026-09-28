using System;

namespace BlackHole.Core
{
    public enum UpgradeOperation
    {
        Add,
        Percent, // 비율을 더한다. 0.25는 +25%.
        Multiply,
    }

    public readonly struct Upgrade
    {
        public string Stat { get; }
        public UpgradeOperation Operation { get; }
        public float Value { get; }

        public Upgrade(string stat, UpgradeOperation operation, float value)
        {
            if (string.IsNullOrWhiteSpace(stat))
                throw new ArgumentException("수치 이름이 비어 있다.", nameof(stat));

            if (!Enum.IsDefined(typeof(UpgradeOperation), operation))
                throw new ArgumentOutOfRangeException(nameof(operation));

            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value), "유한한 값이 필요하다.");

            Stat = stat;
            Operation = operation;
            Value = value;
        }

        public override string ToString() => $"{Stat} {Operation} {Value}";
    }
}
