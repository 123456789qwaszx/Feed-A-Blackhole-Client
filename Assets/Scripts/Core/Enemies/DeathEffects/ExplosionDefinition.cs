using System;

namespace BlackHole.Core
{
    // 폭발(슈퍼노바 별): 죽은 자리를 중심으로 반지름 안에 닿는 적(적의 크기 포함) 전부에게 한 번 피해를 준다.
    // Radius는 크기 1 별의 폭발 반지름이다. 큰 별은 별 반지름과 같은 배율로 넓게 터진다(DeathEffects, SizeRule.RadiusMultiplier).
    // 피해는 고정 수치가 아니라 맞는 순간 대상의 현재 HP × HealthFraction이다(올림, 최소 1. 원작 "현재체력 비례 슈퍼노바 대미지").
    // 기본값은 20%다(GDD 슈퍼노바 별). 100%면 맞은 일반 적은 모두 죽는다.
    public sealed class ExplosionDefinition : DeathEffectDefinition
    {
        public float HealthFraction { get; }
        public float Radius { get; }

        public ExplosionDefinition(float healthFraction, float radius)
        {
            if (float.IsNaN(healthFraction) || healthFraction <= 0 || healthFraction > 1)
                throw new ArgumentOutOfRangeException(nameof(healthFraction), "0보다 크고 1 이하인 값이 필요하다.");

            HealthFraction = healthFraction;
            Radius = DefinitionGuard.Positive(radius, nameof(radius));
        }

        // 대상 하나가 받는 피해.
        internal float DamageTo(Enemy target) => Math.Max(1, (float)Math.Ceiling(target.Health * HealthFraction));
    }
}
