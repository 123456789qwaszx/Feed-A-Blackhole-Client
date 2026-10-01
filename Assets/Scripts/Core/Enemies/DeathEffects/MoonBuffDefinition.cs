namespace BlackHole.Core
{
    // 처치 버프 — 달(행성의 성질): 모든 참가자의 Breaker에 달 중첩 하나를 더한다. 달 중첩은 공격 속도와 공격 범위를 함께 올린다.
    // 중첩의 지속 시간과 중첩당 보너스는 적이 아니라 받는 Breaker의 수치다
    // (BreakerDefinition.MoonDuration·MoonSpeedBonus·MoonRadiusBonus, 판마다 고정).
    // 중첩은 받은 것마다 따로 끝나고, 보너스는 중첩끼리 합연산한 뒤 노드가 반영된 공격 속도·반지름에 곱한다(BreakerSkill).
    // 피해를 만들지 않는다. 받는 참가자는 DeathEffects가 정한다.
    public sealed class MoonBuffDefinition : DeathEffectDefinition
    {
    }
}
