namespace BlackHole.Core
{
    // 질량 단계 하나: 그 종류의 질량 증가를 이만큼 샀을 때 색 등급 표에 곱하는 HP·Gold 계수.
    // 색 비율은 질량 증가와 무관하다 — 블랙홀 성장도가 정한다(StageColorDefinition, GAME_RULES 3.2).
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
