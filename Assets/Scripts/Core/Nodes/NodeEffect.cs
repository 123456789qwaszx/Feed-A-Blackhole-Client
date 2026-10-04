using System;

namespace BlackHole.Core
{
    // 노드 Rank 하나가 주는 효과 하나: StatId 수치에 Value를 더한다.
    // 값은 시트 단위 그대로다(수치의 단위가 Percent면 25 = 25%). 단위는 수치 정의(UpgradeStatDefinition)가 가진다.
    public sealed class NodeEffect
    {
        public string StatId { get; }
        public float Value { get; }

        internal NodeEffect(string statId, float value)
        {
            if (string.IsNullOrWhiteSpace(statId))
                throw new ArgumentException("StatId가 비어 있다.", nameof(statId));

            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value), "유한한 값이 필요하다.");

            StatId = statId;
            Value = value;
        }

        public override string ToString() => $"{StatId} +{Value}";
    }
}
