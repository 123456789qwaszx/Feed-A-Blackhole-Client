using System;

namespace BlackHole.Core
{
    // 황금: 사망 때 Gold에 Multiplier를 곱한다(원작 황금 소행성).
    // 곱한 Gold는 출현 때 그 적의 수치에 이미 들어 있다(EnemyDefinition.StatsAt) — 사망 순간 판의 Gold 합계에 드는 값이 화면·결산과 같게.
    // 그래서 사망 효과 처리(DeathEffects)에서는 할 일이 없다. 배율은 노드(enemy.<종류>.trait.<성질>.multiplier)가 올린다.
    // [후속] 자체 치명타(치명타면 배율 2배)는 아직 없다.
    public sealed class GoldenDefinition : DeathEffectDefinition
    {
        public float Multiplier { get; }
        public float CritChance { get; }
        public float CritRewardScale { get; }

        public GoldenDefinition(float multiplier, float critChance, float critRewardScale)
        {
            Multiplier = DefinitionGuard.Positive(multiplier, nameof(multiplier));
            CritChance = Chance(critChance, nameof(critChance));
            CritRewardScale = DefinitionGuard.Positive(critRewardScale, nameof(critRewardScale)); // 에셋에 임시값 1(100%)로 지정
        }

        private static float Chance(float value, string name)
        {
            if (float.IsNaN(value) || value < 0 || value > 1)
                throw new ArgumentOutOfRangeException(name, "0부터 1까지의 값이 필요하다.");

            return value;
        }
    }
}
