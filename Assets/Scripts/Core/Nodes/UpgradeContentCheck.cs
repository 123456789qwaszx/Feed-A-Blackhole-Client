using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    public static class UpgradeContentCheck
    {
        public static IReadOnlyList<ContentDiagnostic> Check(GameContent content, NodeTree nodes)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));

            if (nodes == null)
                throw new ArgumentNullException(nameof(nodes));

            var diagnostics = new List<ContentDiagnostic>();
            var everything = new List<Upgrade>();

            foreach (NodeDefinition node in nodes.Nodes)
                everything.AddRange(node.Upgrades);

            var table = new UpgradeTable(everything);
            long extraSupply = 0;

            if (content.Breaker != null)
            {
                try
                {
                    content.Breaker.Upgraded(table);
                }
                catch (ArgumentException error)
                {
                    diagnostics.Add(new ContentDiagnostic("Nodes(모두 산 경우).Breaker", error.Message));
                }
            }

            try
            {
                HqUpgradeStats.GrowthTimeFrom(table);
            }
            catch (ArgumentException error)
            {
                diagnostics.Add(new ContentDiagnostic("Nodes(모두 산 경우).Hq", error.Message));
            }

            EnemyContent enemies = content.Enemies;
            bool growthSupply = false;
            var compositions = new Dictionary<EnemyDefinition, EnemyComposition>();

            foreach (EnemyDefinition kind in enemies.Enemies)
            {
                try
                {
                    EnemyComposition composition = EnemyComposition.From(kind, table);
                    extraSupply += composition.StartSupplyBonus;
                    growthSupply |= composition.GrowthSupply > 0;
                    compositions.Add(kind, composition);
                }
                catch (ArgumentException error)
                {
                    diagnostics.Add(new ContentDiagnostic($"Nodes(모두 산 경우).Enemies[{kind.Id}]", error.Message));
                }
            }

            // 한 부모의 특수 종류 생성 확률 합은 100%를 넘을 수 없다. 종류마다의 판 구성이 모두 계산될 때만 본다.
            if (compositions.Count == enemies.Enemies.Count)
            {
                try
                {
                    new EnemyStatTable(enemies.Enemies, compositions);
                }
                catch (ArgumentException error)
                {
                    diagnostics.Add(new ContentDiagnostic("Nodes(모두 산 경우).Enemies", error.Message));
                }
            }

            if ((extraSupply > 0 || growthSupply) && enemies.EnemyPlacement == null)
            {
                diagnostics.Add(new ContentDiagnostic(
                    "Nodes(모두 산 경우).EnemyPlacement", "공급 수 노드가 있으면 출현 배치가 필요하다."));
            }

            EnemyContentInvariants.CheckStartSupplyFits(
                enemies.StartSupply,
                extraSupply,
                enemies.MaxAliveEnemies,
                diagnostics);

            return diagnostics.AsReadOnly();
        }
    }
}
