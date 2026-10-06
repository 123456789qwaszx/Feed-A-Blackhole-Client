using System;

namespace BlackHole.Core
{
    public sealed class NodeEffect
    {
        public string StatId { get; }
        // 시트 단위 그대로다. 단위는 수치 정의(UpgradeStatDefinition)가 가진다.
        public float Value { get; }

        internal NodeEffect(string statId, float value)
        {
            StatId = statId ?? throw new ArgumentNullException(nameof(statId));
            Value = value;
        }
    }
}
