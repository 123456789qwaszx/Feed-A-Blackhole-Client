namespace BlackHole.Core
{
    // 질량 단계 하나:
    // - 특정 종류의 질량 증가 구매 시, 색 등급 표에 곱하는 HP·Gold 계수.
    public sealed class MassLevelDefinition
    {
        public float HealthMultiplier { get; }
        public float GoldMultiplier { get; }

        public MassLevelDefinition(float healthMultiplier, float goldMultiplier)
        {
            HealthMultiplier = DefinitionGuard.Positive(healthMultiplier, nameof(healthMultiplier));
            GoldMultiplier = DefinitionGuard.Positive(goldMultiplier, nameof(goldMultiplier));
        }
    }
}
