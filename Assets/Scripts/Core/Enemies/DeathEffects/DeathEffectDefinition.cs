namespace BlackHole.Core
{
    // 사망 효과의 정의. 적 종류에 붙는 특성이다 — 종류가 아니다(전기 소행성은 연쇄 번개를 가진 적 종류다).
    // 효과를 가진 적은 사망 효과의 피해를 받지 않는다(GAME_RULES 10절). 그래서 효과가 효과를 부르지 않는다.
    // 새 효과: 하위 정의 + DeathEffects.Resolve 분기 + EnemyContentLoader의 종류 이름.
    public abstract class DeathEffectDefinition
    {
        private protected DeathEffectDefinition() { }
    }
}
