using System;

namespace BlackHole.Core
{
    // 시간제 종료의 공유 정의.
    public sealed class TimeLimitDefinition
    {
        public float Duration { get; }

        // 적이 파괴될 때 시간 추가가 성공하면(판 구성의 TimeChance) 제한 시간에 더하는 초.
        public float KillTimeBonus { get; }

        public TimeLimitDefinition(float duration, float killTimeBonus = 0)
        {
            Duration = DefinitionGuard.Positive(duration, nameof(duration));

            if (float.IsNaN(killTimeBonus) || float.IsInfinity(killTimeBonus) || killTimeBonus < 0)
                throw new ArgumentOutOfRangeException(nameof(killTimeBonus), "0 이상의 유한한 값이 필요하다.");

            KillTimeBonus = killTimeBonus;
        }

        // 산 노드의 수치 값으로 이 판의 제한 시간을 계산함.
        public TimeLimitDefinition Upgraded(UpgradeStatValues upgrades)
        {
            float duration = Duration + upgrades.GainOf(UpgradeStat.Timer);

            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(upgrades), $"제한 시간은 0보다 큰 유한한 값이어야 한다. 노드 반영 값: {duration}.");

            return new TimeLimitDefinition(duration, KillTimeBonus);
        }
    }
}
