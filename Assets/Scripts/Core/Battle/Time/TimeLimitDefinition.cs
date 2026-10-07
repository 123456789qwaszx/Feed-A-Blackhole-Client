using System;

namespace BlackHole.Core
{
    // 시간제 종료의 공유 정의. 시간제는 현재 후보이며 최종 종료 조건은 미정이다.
    public sealed class TimeLimitDefinition
    {
        public float Duration { get; }

        // 적이 파괴될 때 시간 추가가 성공하면(판 구성의 TimeChance) 제한 시간에 더하는 초. 원작 실측 0.3초.
        public float KillTimeBonus { get; }

        public TimeLimitDefinition(float duration, float killTimeBonus = 0)
        {
            Duration = DefinitionGuard.Positive(duration, nameof(duration));

            if (float.IsNaN(killTimeBonus) || float.IsInfinity(killTimeBonus) || killTimeBonus < 0)
                throw new ArgumentOutOfRangeException(nameof(killTimeBonus), "0 이상의 유한한 값이 필요하다.");

            KillTimeBonus = killTimeBonus;
        }

        // 산 노드의 수치 값으로 이 판의 제한 시간을 계산한다: 이 정의의 값 + 노드로 늘어난 초(UpgradeStat.Timer, 원작 "세션 타이머").
        // 0 이하·무한은 예외다 — 노드 저작 오류이며 UpgradeContentCheck가 로드 때 찾는다.
        public TimeLimitDefinition Upgraded(UpgradeStatValues upgrades)
        {
            float duration = Duration + upgrades.GainOf(UpgradeStat.Timer);

            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0)
                throw new ArgumentOutOfRangeException(nameof(upgrades), $"제한 시간은 0보다 큰 유한한 값이어야 한다. 노드 반영 값: {duration}.");

            return new TimeLimitDefinition(duration, KillTimeBonus);
        }
    }
}
