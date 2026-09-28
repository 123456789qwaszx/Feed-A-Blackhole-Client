namespace BlackHole.Core
{
    // 색 등급 하나:
    // - 한 종류 안의 색 하나. 색이 크기·기본 HP·기본 Gold·EXP를 정함.
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
