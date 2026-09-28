using System;

namespace BlackHole.Core
{
    // 처치 버프 — 공격 주기 감소: Duration초 동안 Breaker의 공격 주기가 IntervalMultiplier배가 된다(0.5면 절반).
    // 원작 달은 "파괴하면 일정 시간 Breaker가 강화된다"(R7·R9). 강화가 공격 주기 감소라는 것은 사용자 설명에 맞춘 해석이다 [분석].
    // 피해를 만들지 않는다. 받는 참가자는 DeathEffects가 정한다.
    public sealed class AttackHasteDefinition : DeathEffectDefinition
    {
        public float Duration { get; }
        public float IntervalMultiplier { get; }

        public AttackHasteDefinition(float duration, float intervalMultiplier)
        {
            Duration = DefinitionGuard.Positive(duration, nameof(duration));

            if (float.IsNaN(intervalMultiplier) || intervalMultiplier <= 0 || intervalMultiplier >= 1)
                throw new ArgumentOutOfRangeException(nameof(intervalMultiplier), "0보다 크고 1보다 작은 값이 필요하다.");

            IntervalMultiplier = intervalMultiplier;
        }
    }
}
