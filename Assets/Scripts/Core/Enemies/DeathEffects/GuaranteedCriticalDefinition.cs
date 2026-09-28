namespace BlackHole.Core
{
    // 처치 버프 — 확정 치명타: Duration초 동안 Breaker의 Tick이 모두 치명타다(원작 혜성: 커서 공격이 항상 치명타, R8).
    // 배율은 Breaker의 CritMultiplier다. 이 효과가 배율을 따로 갖지 않는다. 피해를 만들지 않는다.
    public sealed class GuaranteedCriticalDefinition : DeathEffectDefinition
    {
        public float Duration { get; }

        public GuaranteedCriticalDefinition(float duration)
        {
            Duration = DefinitionGuard.Positive(duration, nameof(duration));
        }
    }
}
