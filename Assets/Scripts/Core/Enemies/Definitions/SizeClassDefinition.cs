namespace BlackHole.Core
{
    // 크기 등급 하나(원작 소행성 크기1·2·3):
    // - 같은 종류·같은 색 안에서 생성 때 정해지는 크기. 색 등급의 크기·HP·Gold·EXP에 곱하는 계수.
    // - 크기 노드를 산 수만큼 다음 등급이 열리고, 열린 등급이 섞여 나온다(EnemyComposition.SizeLevel).
    public sealed class SizeClassDefinition
    {
        // 크기 등급이 없는 종류가 쓰는 한 줄: 모든 계수가 1.
        public static readonly SizeClassDefinition Base = new SizeClassDefinition(1, 1, 1, 1);

        public float SizeMultiplier { get; }
        public float HealthMultiplier { get; }
        public float GoldMultiplier { get; }
        public float ExpMultiplier { get; }

        public SizeClassDefinition(float sizeMultiplier, float healthMultiplier, float goldMultiplier, float expMultiplier)
        {
            SizeMultiplier = DefinitionGuard.Positive(sizeMultiplier, nameof(sizeMultiplier));
            HealthMultiplier = DefinitionGuard.Positive(healthMultiplier, nameof(healthMultiplier));
            GoldMultiplier = DefinitionGuard.Positive(goldMultiplier, nameof(goldMultiplier));
            ExpMultiplier = DefinitionGuard.Positive(expMultiplier, nameof(expMultiplier));
        }
    }
}
