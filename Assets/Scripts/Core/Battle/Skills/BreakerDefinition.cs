using System;

namespace BlackHole.Core
{
    public sealed class BreakerDefinition
    {
        public float Damage { get; }
        public float Interval { get; } // 공격 주기(초).
        public float Radius { get; } // 공격 원의 기본 반지름(노드 반영).
        public float CritChance { get; }
        public float CritDamage { get; }
        public float MoonDuration { get; } // 달 버프 중첩 하나의 지속 시간(초)
        public float MoonSpeedBonus { get; }
        public float MoonRadiusBonus { get; }
        public float CometDuration { get; } // 혜성 버프 중첩 하나의 지속 시간(초)
        public float CometCritDamageBonus { get; }
        public float PlanetBonusDamage { get; } // 행성 보너스 피해
        public float StarBonusDamage { get; } // 별 보너스 피해

        public BreakerDefinition(
            float damage,
            float interval,
            float radius,
            float critChance,
            float critDamage,
            float moonDuration,
            float moonSpeedBonus,
            float moonRadiusBonus,
            float cometDuration,
            float cometCritDamageBonus,
            float planetBonusDamage,
            float starBonusDamage)
        {
            Damage = DefinitionGuard.Positive(damage, nameof(damage));
            Interval = DefinitionGuard.Positive(interval, nameof(interval));
            Radius = DefinitionGuard.Positive(radius, nameof(radius));

            if (float.IsNaN(critChance) || critChance < 0 || critChance > 1)
                throw new ArgumentOutOfRangeException(nameof(critChance), "0부터 1까지의 값이 필요하다.");

            CritChance = critChance;
            CritDamage = NotNegative(critDamage, nameof(critDamage));
            MoonDuration = DefinitionGuard.Positive(moonDuration, nameof(moonDuration));
            MoonSpeedBonus = NotNegative(moonSpeedBonus, nameof(moonSpeedBonus));
            MoonRadiusBonus = NotNegative(moonRadiusBonus, nameof(moonRadiusBonus));
            CometDuration = DefinitionGuard.Positive(cometDuration, nameof(cometDuration));
            CometCritDamageBonus = NotNegative(cometCritDamageBonus, nameof(cometCritDamageBonus));
            PlanetBonusDamage = NotNegative(planetBonusDamage, nameof(planetBonusDamage));
            StarBonusDamage = NotNegative(starBonusDamage, nameof(starBonusDamage));
        }

        private static float NotNegative(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0)
                throw new ArgumentOutOfRangeException(name, "0 이상의 유한한 값이 필요하다.");
            return value;
        }

        public BreakerDefinition Upgraded(UpgradeStatValues upgrades)
        {
            float speed = 1 + upgrades.GainOf(UpgradeStat.BreakerSpeed) / 100;

            if (float.IsNaN(speed) || float.IsInfinity(speed) || speed <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(upgrades), $"Breaker 공격 속도는 0보다 커야 한다. 노드 반영 값: {speed}.");

            return new BreakerDefinition(
                Damage + upgrades.GainOf(UpgradeStat.BreakerDamage),
                Interval / speed,
                Radius * (1 + upgrades.GainOf(UpgradeStat.BreakerRadius) / 100),
                Math.Min(1, CritChance + upgrades.GainOf(UpgradeStat.BreakerCritChance) / 100),
                CritDamage + upgrades.GainOf(UpgradeStat.BreakerCritBonus) / 100,
                MoonDuration + upgrades.GainOf(UpgradeStat.MoonPlanetDuration),
                MoonSpeedBonus + upgrades.GainOf(UpgradeStat.MoonPlanetSpeedScale) / 100,
                MoonRadiusBonus + upgrades.GainOf(UpgradeStat.MoonPlanetRangeScale) / 100,
                CometDuration + upgrades.GainOf(UpgradeStat.CometDuration),
                CometCritDamageBonus + upgrades.GainOf(UpgradeStat.CometCritBonus) / 100,
                PlanetBonusDamage + upgrades.GainOf(UpgradeStat.BreakerPlanetBonusDamage),
                StarBonusDamage + upgrades.GainOf(UpgradeStat.BreakerStarBonusDamage));
        }
    }
}
