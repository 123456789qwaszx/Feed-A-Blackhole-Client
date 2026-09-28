namespace BlackHole.Core
{
    // 색 등급 하나: 한 종류 안의 색 하나(BATTLE_COMPOSITION_PLAN 4.1). 색이 크기·기본 HP·기본 Gold·EXP를 정한다.
    // 원작의 돈과 EXP는 색마다 비선형이라 공식을 두지 않고 색마다 숫자를 적는다.
    // 색(외형) 자체는 Core가 모른다 — Unity 쪽 종류 에셋의 같은 번호 줄이 가진다.
    public readonly struct EnemyTier
    {
        public float MaxHealth { get; }
        public float Size { get; }
        public long Gold { get; }
        public long Exp { get; }

        public EnemyTier(float maxHealth, float size, long gold, long exp = 0)
        {
            MaxHealth = DefinitionGuard.Positive(maxHealth, nameof(maxHealth));
            Size = DefinitionGuard.Positive(size, nameof(size));
            Gold = DefinitionGuard.NotNegative(gold, nameof(gold));
            Exp = DefinitionGuard.NotNegative(exp, nameof(exp));
        }
    }
}
