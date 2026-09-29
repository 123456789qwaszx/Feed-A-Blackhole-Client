namespace BlackHole.Core
{
    // 폭발: 죽은 자리를 중심으로 Radius 안에 닿는 적(적의 크기 포함) 전부에게 한 번 피해를 준다(원작 초신성으로 보임, 피해 공식 미확인 [임시]).
    public sealed class ExplosionDefinition : DeathEffectDefinition
    {
        public float Damage { get; }
        public float Radius { get; }

        public ExplosionDefinition(float damage, float radius)
        {
            Damage = DefinitionGuard.Positive(damage, nameof(damage));
            Radius = DefinitionGuard.Positive(radius, nameof(radius));
        }
    }
}
