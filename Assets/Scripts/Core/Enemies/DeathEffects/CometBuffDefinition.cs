namespace BlackHole.Core
{
    // 처치 버프 — 혜성: 모든 참가자의 Breaker에 혜성 중첩 하나를 더한다(원작 혜성: 커서 공격이 항상 치명타, R8).
    // 중첩이 하나라도 있으면 Breaker의 Tick이 모두 치명타다. 중첩은 받은 것마다 따로 끝난다.
    // 중첩의 지속 시간과 치명타 피해 보너스 증가는 적이 아니라 받는 Breaker의 수치다
    // (BreakerDefinition.CometDuration·CometCritDamageBonus, 판마다 고정).
    // 치명타 피해 보너스 = 노드가 반영된 치명타 피해 보너스 × (1 + 중첩 보너스 합). 첫 중첩부터 적용한다(BreakerSkill). 피해를 만들지 않는다.
    public sealed class CometBuffDefinition : DeathEffectDefinition
    {
    }
}
