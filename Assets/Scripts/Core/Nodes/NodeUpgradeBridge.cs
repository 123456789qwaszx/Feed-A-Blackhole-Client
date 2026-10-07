using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // [임시 다리] 시트 수치(UpgradeStats)의 값을 전투 쪽이 지금 읽는 옛 업그레이드 표(UpgradeTable, 옛 수치 이름)로 옮긴다.
    // 전투 쪽이 시트 수치(UpgradeStatValues)를 직접 읽게 되면 지운다.
    //
    // 넘기는 것은 "기본값에서 늘어난 양"(하한·상한으로 자른 값 − 기본값)이다. 기본값 자체는 전투 쪽 콘텐츠(Skills·적 종류 시트)의 값을 그대로 쓴다.
    // 단위는 옛 표의 것으로 바꾼다: 옛 표의 확률·보너스는 0.25 = 25%(× 0.01), 적 성질·픽업 확률은 25 = 25%(EnemyComposition이 ÷100),
    // 성장 공급은 25 = 시작 수의 25%(판 조립이 시작 수와 곱한다). 제한 시간·천체 변환은 그대로(초, 마리 수).
    // 범위·속도·너비(기본 100%)는 옛 표의 Percent(기본값 × (1 + 늘어난 비율))로, 나머지는 Add로 넘긴다.
    // 사망 효과의 치명타 보너스는 시트 200% = 배율 ×2라 늘어난 %를 배율에 더한다(× 0.01). 기본 배율은 적 종류 에셋의 값이다.
    // 적 질량(massScale)은 시트 값이 곧 전투 질량이다(%, MassRule). 그래서 시트 기본값이 아니라 전투 기본(MassRule.Base)에서 늘어난 양을 넘긴다
    // — 원작 행성 질량은 0%에서 시작한다(0 ~ 100%는 빨강만이라 시작은 같고, 첫 노드 110%에서 주황이 나오기 시작한다).
    //
    // 여기 없는 수치는 노드를 사도 아직 전투에 효과가 없다. 옛 이름이 없거나(구현 필요), 뜻이 달라 그대로 옮길 수 없는 수치다:
    // goldenAsteroid.rewardScale(시트 50% ↔ 전투 ×50).
    public static class NodeUpgradeBridge
    {
        private const float Percent = 0.01f;

        private static readonly Route[] _routes =
        {
            new Route("timer", SessionUpgradeStats.TimeLimit, UpgradeOperation.Add, 1),
            new Route("growth.time", HqUpgradeStats.GrowthTime, UpgradeOperation.Add, 1),
            new Route("growth.asteroids", EnemyUpgradeStats.GrowthSupply("asteroid"), UpgradeOperation.Add, 1),
            new Route("growth.planets", EnemyUpgradeStats.GrowthSupply("planet"), UpgradeOperation.Add, 1),
            new Route("growth.stars", EnemyUpgradeStats.GrowthSupply("star"), UpgradeOperation.Add, 1),
            new Route("breaker.damage", BreakerUpgradeStats.Damage, UpgradeOperation.Add, 1),
            new Route("breaker.radius", BreakerUpgradeStats.Radius, UpgradeOperation.Percent, Percent),
            new Route("breaker.speed", BreakerUpgradeStats.Speed, UpgradeOperation.Percent, Percent),
            new Route("breaker.critChance", BreakerUpgradeStats.CritChance, UpgradeOperation.Add, Percent),
            new Route("breaker.critBonus", BreakerUpgradeStats.CritDamage, UpgradeOperation.Add, Percent),
            new Route("asteroid.count", EnemyUpgradeStats.StartSupply("asteroid"), UpgradeOperation.Add, 1),
            new Route("asteroid.size", EnemyUpgradeStats.Size("asteroid"), UpgradeOperation.Add, 1),
            Route.FromBase("asteroid.massScale", EnemyUpgradeStats.Mass("asteroid"), MassRule.Base),
            new Route("planet.size", EnemyUpgradeStats.Size("planet"), UpgradeOperation.Add, 1),
            Route.FromBase("planet.massScale", EnemyUpgradeStats.Mass("planet"), MassRule.Base),
            new Route("planet.respawnChance", EnemyUpgradeStats.RespawnChance("planet"), UpgradeOperation.Add, 1),
            new Route("planet.timeChance", EnemyUpgradeStats.TimeChance("planet"), UpgradeOperation.Add, 1),
            new Route("star.size", EnemyUpgradeStats.Size("star"), UpgradeOperation.Add, 1),
            Route.FromBase("star.massScale", EnemyUpgradeStats.Mass("star"), MassRule.Base),
            new Route("star.respawnChance", EnemyUpgradeStats.RespawnChance("star"), UpgradeOperation.Add, 1),
            new Route("star.timeChance", EnemyUpgradeStats.TimeChance("star"), UpgradeOperation.Add, 1),
            new Route("asteroid.toPlanet", EnemyUpgradeStats.Upgrade("asteroid"), UpgradeOperation.Add, 1),
            new Route("planet.toStar", EnemyUpgradeStats.Upgrade("planet"), UpgradeOperation.Add, 1),
            new Route("electricAsteroid.spawnChance", EnemyUpgradeStats.TraitChance("asteroid", "electric"), UpgradeOperation.Add, 1),
            new Route("goldenAsteroid.spawnChance", EnemyUpgradeStats.TraitChance("asteroid", "golden"), UpgradeOperation.Add, 1),
            new Route("moonPlanet.spawnChance", EnemyUpgradeStats.TraitChance("planet", "moon"), UpgradeOperation.Add, 1),
            new Route("moonPlanet.speedScale", BreakerUpgradeStats.MoonSpeedBonus, UpgradeOperation.Add, Percent),
            new Route("moonPlanet.rangeScale", BreakerUpgradeStats.MoonRadiusBonus, UpgradeOperation.Add, Percent),
            new Route("moonPlanet.duration", BreakerUpgradeStats.MoonDuration, UpgradeOperation.Add, 1),
            new Route("moonPlanet.maxCount", EnemyUpgradeStats.TraitMaxAlive("planet", "moon"), UpgradeOperation.Add, 1),
            new Route("comet.spawnChance", EnemyUpgradeStats.Chance("comet"), UpgradeOperation.Add, 1),
            new Route("comet.duration", BreakerUpgradeStats.CometDuration, UpgradeOperation.Add, 1),
            new Route("comet.critBonus", BreakerUpgradeStats.CometCritDamageBonus, UpgradeOperation.Add, Percent),
            new Route("comet.rainChance", EnemyUpgradeStats.RainChance("comet"), UpgradeOperation.Add, 1),
            new Route("electricStar.spawnChance", EnemyUpgradeStats.TraitChance("star", "electric"), UpgradeOperation.Add, 1),
            new Route("electricStar.damage", EnemyUpgradeStats.TraitDamage("star", "electric"), UpgradeOperation.Add, 1),
            new Route("electricStar.critChance", EnemyUpgradeStats.TraitCritChance("star", "electric"), UpgradeOperation.Add, Percent),
            new Route("electricStar.chain", EnemyUpgradeStats.TraitMaxTargets("star", "electric"), UpgradeOperation.Add, 1),
            new Route("electricStar.splitChance", EnemyUpgradeStats.TraitBranchChance("star", "electric"), UpgradeOperation.Add, Percent),
            new Route("electricStar.critBonus", EnemyUpgradeStats.TraitCritMultiplier("star", "electric"), UpgradeOperation.Add, Percent),
            new Route("laserStar.spawnChance", EnemyUpgradeStats.TraitChance("star", "laser"), UpgradeOperation.Add, 1),
            new Route("laserStar.width", EnemyUpgradeStats.TraitWidth("star", "laser"), UpgradeOperation.Percent, Percent),
            new Route("laserStar.critChance", EnemyUpgradeStats.TraitCritChance("star", "laser"), UpgradeOperation.Add, Percent),
            new Route("laserStar.damage", EnemyUpgradeStats.TraitDamage("star", "laser"), UpgradeOperation.Add, 1),
            new Route("laserStar.critBonus", EnemyUpgradeStats.TraitCritMultiplier("star", "laser"), UpgradeOperation.Add, Percent),
            new Route("supernovaStar.spawnChance", EnemyUpgradeStats.TraitChance("star", "supernova"), UpgradeOperation.Add, 1),
            new Route("supernovaStar.range", EnemyUpgradeStats.TraitRadius("star", "supernova"), UpgradeOperation.Percent, Percent),
            new Route("supernovaStar.hpDamage", EnemyUpgradeStats.TraitHealthFraction("star", "supernova"), UpgradeOperation.Add, Percent),
            new Route("breaker.planetBonus", BreakerUpgradeStats.PlanetBonus, UpgradeOperation.Add, 1),
            new Route("breaker.starBonus", BreakerUpgradeStats.StarBonus, UpgradeOperation.Add, 1),
            new Route("asteroid.respawnChance", EnemyUpgradeStats.RespawnChance("asteroid"), UpgradeOperation.Add, 1),
            new Route("asteroid.timeChance", EnemyUpgradeStats.TimeChance("asteroid"), UpgradeOperation.Add, 1),
            new Route("electricAsteroid.damage", EnemyUpgradeStats.TraitDamage("asteroid", "electric"), UpgradeOperation.Add, 1),
            new Route("electricAsteroid.critChance", EnemyUpgradeStats.TraitCritChance("asteroid", "electric"), UpgradeOperation.Add, Percent),
            new Route("electricAsteroid.chain", EnemyUpgradeStats.TraitMaxTargets("asteroid", "electric"), UpgradeOperation.Add, 1),
            new Route("electricAsteroid.splitChance", EnemyUpgradeStats.TraitBranchChance("asteroid", "electric"), UpgradeOperation.Add, Percent),
            new Route("electricAsteroid.critBonus", EnemyUpgradeStats.TraitCritMultiplier("asteroid", "electric"), UpgradeOperation.Add, Percent),
            new Route("goldenAsteroid.critChance", EnemyUpgradeStats.TraitCritChance("asteroid", "golden"), UpgradeOperation.Add, Percent),
            new Route("goldenAsteroid.critRewardScale", EnemyUpgradeStats.TraitCritRewardScale("asteroid", "golden"), UpgradeOperation.Add, Percent),
        };

        private static readonly HashSet<string> _routed = RoutedStats();

        // 이 수치가 전투에 이어져 있는가(노드를 사면 지금 효과가 있는가).
        public static bool IsRouted(string statId) => _routed.Contains(statId);

        public static UpgradeTable ToUpgradeTable(UpgradeStatValues values)
        {
            var upgrades = new List<Upgrade>();

            foreach (Route route in _routes)
            {
                // 시트에서 수치가 빠졌으면 옮길 것이 없다.
                if (!values.Has(route.StatId))
                    continue;

                float from = route.Base ?? values.DefinitionOf(route.StatId).DefaultValue;
                float gained = values.ValueOf(route.StatId) - from;

                if (gained != 0)
                    upgrades.Add(new Upgrade(route.Target, route.Operation, gained * route.Scale));
            }

            return new UpgradeTable(upgrades);
        }

        private static HashSet<string> RoutedStats()
        {
            var routed = new HashSet<string>(StringComparer.Ordinal);

            foreach (Route route in _routes)
                routed.Add(route.StatId);

            return routed;
        }

        private readonly struct Route
        {
            public readonly string StatId;
            public readonly string Target;
            public readonly UpgradeOperation Operation;
            public readonly float Scale;
            // 있으면 시트 기본값 대신 이 값에서 늘어난 양을 넘긴다(시트 값이 곧 전투 값인 수치, 전투 기본값).
            public readonly float? Base;

            public Route(string statId, string target, UpgradeOperation operation, float scale, float? fromBase = null)
            {
                StatId = statId;
                Target = target;
                Operation = operation;
                Scale = scale;
                Base = fromBase;
            }

            // 시트 값을 그대로 전투 값으로: 전투 기본값 battleBase에서 늘어난 양을 더한다.
            public static Route FromBase(string statId, string target, float battleBase) =>
                new Route(statId, target, UpgradeOperation.Add, 1, battleBase);
        }
    }
}
