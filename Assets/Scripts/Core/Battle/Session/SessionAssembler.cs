using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 진행 상태는 방장의 것 하나다:
    // - 방장이 노드를 사고(Gold·산 노드), 그 결과가 판 전체에 반영됨.
    // 스탯과 처치 버프는 판 안의 모든 참가자가 함께 받음.
    //
    // 조립한 판은 준비 단계(Preparing)다:
    // - 이 판의 판 구성과 적 수치를 확정하고, 판 안의 참가자(조준점·스킬)를 만듬.
    public static class SessionAssembler
    {
        public const int DefaultSeed = 0;

        public static GameSession CreateBattle(GameContent content, PlayerState progress) =>
            CreateBattle(content, progress, DefaultSeed);

        public static GameSession CreateBattle(GameContent content, PlayerState progress, int seed, UpgradeTable upgrades = null)
        {
            UpgradeTable table = upgrades ?? new UpgradeTable(Array.Empty<Upgrade>());
            EnemyContent enemies = content.Enemies;

            var battlePlayers = new List<BattlePlayer>
            {
                new BattlePlayer(
                    progress.Id,
                    content.Breaker?.Upgraded(table),
                    content.Laser,
                    seed),
            };

            // 이 판의 블랙홀: Level 0에서 시작하고, 성장도 기반 Level 표를 고른다.
            var hq = new Hq(content.Growth, HqUpgradeStats.GrowthTimeFrom(table), progress.GrowthStage);

            // 전투 Session이 시작되기 전,
            // 적의 수치(Gold 포함)와 색·황금 비율을 결정해둠.
            // 색 비율은 성장도에 의해 결정.
            var stats = new EnemyStatTable(
                enemies.Enemies,
                CompositionsOf(enemies, table, hq.Stage),
                hq.Stage);

            var world = new World(
                seed,
                stats,
                enemies.EnemyPlacement,
                enemies.MaxAliveEnemies,
                hq,
                battlePlayers);

            return new GameSession(
                world,
                new TimeLimitRule(content.TimeLimit),
                seed,
                progress,
                table,
                StartSupplyOf(enemies, stats));
        }

        // 적 종류마다의 판 구성.
        private static Dictionary<EnemyDefinition, EnemyComposition> CompositionsOf(
            EnemyContent enemies,
            UpgradeTable upgrades,
            int stage)
        {
            var compositions = new Dictionary<EnemyDefinition, EnemyComposition>();

            foreach (EnemyDefinition kind in enemies.Enemies)
                compositions.Add(kind, EnemyComposition.From(kind, upgrades, stage));

            return compositions;
        }

        // 기본 시작 적 목록에,
        // 업그레이드로 추가된 시작 적 수를 합쳐서,
        // 최종 시작시 등장 할 적 목록을 만듬.
        private static IReadOnlyList<SupplyRequest> StartSupplyOf(
            EnemyContent enemies,
            EnemyStatTable stats)
        {
            var supply = new List<SupplyRequest>();
            var bonused = new HashSet<EnemyDefinition>();

            foreach (SupplyRequest request in enemies.StartSupply)
            {
                int bonus = stats.CompositionOf(request.Enemy).StartSupplyBonus;

                if (bonus > 0 && bonused.Add(request.Enemy))
                    supply.Add(new SupplyRequest(request.Enemy, request.Count + bonus));
                else
                    supply.Add(request);
            }

            foreach (EnemyDefinition kind in stats.Kinds)
            {
                EnemyComposition composition = stats.CompositionOf(kind);
                int bonus = composition.StartSupplyBonus;

                if (bonus > 0 && bonused.Add(kind))
                    supply.Add(new SupplyRequest(kind, bonus));
            }

            return supply.AsReadOnly();
        }
    }
}
