namespace BlackHole.Core
{
    // 사망 효과의 정의. 특수 성질(EnemyTraitDefinition)이 가진다 — 종류가 아니다(전기 소행성은 전기 성질이 붙은 소행성이다).
    // 성질이 붙은 적(특수 적)은 사망 효과의 피해를 받지 않는다. 그래서 효과가 효과를 부르지 않는다.
    // 새 효과: 하위 정의 + DeathEffects.Resolve 분기 + EnemyContentLoader의 종류 이름.
    public abstract class DeathEffectDefinition
    {
        private protected DeathEffectDefinition() { }
    }
}
