using System;

namespace BlackHole.Core
{
    // 폭발(슈퍼노바 별): 죽은 자리를 중심으로 Radius 안에 닿는 적(적의 크기 포함) 전부에게 한 번 피해를 준다.
    // 피해는 고정 수치가 아니라 대상 최대 HP의 HealthFraction이다(올림, 최소 1). 기본값은 20%다(GDD 슈퍼노바 별).
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
        internal float DamageTo(Enemy target) => Math.Max(1, (float)Math.Ceiling(target.Stats.MaxHealth * HealthFraction));
    }
}
