using System;

namespace BlackHole.Core
{
    // 레이저 별: 죽은 자리에서 무작위 방향의 직선 레이저를 쏘아 경로 위의 적 전부에게 피해를 준다(GDD 레이저 별).
    // [후속] 지금은 발동 기록(LaserBurst)만 남기고 피해는 주지 않는다. 수치는 정의와 검증까지만 있다.
    public sealed class LaserBurstDefinition : DeathEffectDefinition
    {
        public float Damage { get; }
        // 레이저의 너비(경로 양쪽 거리의 합).
        public float Width { get; }
        public float CritChance { get; }
        // 치명타일 때 피해에 곱하는 값.
        public float CritMultiplier { get; }

        public LaserBurstDefinition(float damage, float width, float critChance, float critMultiplier)
        {
            Damage = DefinitionGuard.Positive(damage, nameof(damage));
            Width = DefinitionGuard.Positive(width, nameof(width));

            if (float.IsNaN(critChance) || critChance < 0 || critChance > 1)
                throw new ArgumentOutOfRangeException(nameof(critChance), "0부터 1까지의 값이 필요하다.");

            CritChance = critChance;
            CritMultiplier = DefinitionGuard.Positive(critMultiplier, nameof(critMultiplier));
        }
    }
}
