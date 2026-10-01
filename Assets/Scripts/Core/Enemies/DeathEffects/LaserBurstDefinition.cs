using System;

namespace BlackHole.Core
{
    // 레이저 별: 죽은 자리를 지나는 무작위 방향의 직선 레이저가 화면을 가로질러 경로 위의 적 전부에게 피해를 준다.
    public sealed class LaserBurstDefinition : DeathEffectDefinition
    {
        public float Damage { get; }
        public float Width { get; } // 레이저의 너비(경로 양쪽 거리의 합).
        public float CritChance { get; }
        public float CritMultiplier { get; } // 치명타일 때 피해에 곱하는 값.

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
