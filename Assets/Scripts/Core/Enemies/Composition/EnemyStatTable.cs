using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 적 스탯:
    // - 종류 별 구성(질량 단계·황금 비율·황금 배율)
    // - 색 비율(판을 시작할 때의 블랙홀 성장도)
    //
    // 전투 Session이 시작되기 전, 미리 값을 한 번 정하여 사용.
    // 출현하는 모든 적은 이 표의 수치를 받는다.
    public sealed class EnemyStatTable
    {
        private readonly Dictionary<EnemyDefinition, Row> _rows = new Dictionary<EnemyDefinition, Row>();
        private readonly Dictionary<EnemyDefinition, EnemyDefinition> _upgradeTargets = new Dictionary<EnemyDefinition, EnemyDefinition>();
        private readonly Dictionary<EnemyDefinition, List<EnemyDefinition>> _specials = new Dictionary<EnemyDefinition, List<EnemyDefinition>>();

        // 블랙홀의 성장도. (행성의 색 비율을 결정)
        public int Stage { get; }

        public IReadOnlyList<EnemyDefinition> Kinds { get; }

        internal EnemyStatTable(
            IReadOnlyList<EnemyDefinition> enemies,
            IReadOnlyDictionary<EnemyDefinition, EnemyComposition> compositions,
            int stage = HqGrowthDefinition.StartStage)
        {
            Stage = stage;
            var kinds = new EnemyDefinition[enemies.Count];

            for (int i = 0; i < kinds.Length; i++)
            {
                EnemyDefinition kind = enemies[i];
                EnemyComposition composition = compositions != null && compositions.TryGetValue(kind, out EnemyComposition chosen)
                    ? chosen
                    : EnemyComposition.Base(kind);

                if (composition.GoldenRatio > 0 && !kind.CanBeGolden)
                    throw new ArgumentException($"'{kind.Id}'는 황금이 되지 않는다.", nameof(compositions));

                // [크기 등급][색 등급]. 크기 등급은 이 판에 열린 것(0 ~ SizeLevel)만 있다.
                var stats = new EnemyStats[composition.SizeLevel + 1][];
                EnemyStats[][] goldenStats = kind.CanBeGolden ? new EnemyStats[stats.Length][] : null;

                for (int size = 0; size < stats.Length; size++)
                {
                    stats[size] = new EnemyStats[kind.Tiers.Count];

                    if (goldenStats != null)
                        goldenStats[size] = new EnemyStats[kind.Tiers.Count];

                    for (int tier = 0; tier < kind.Tiers.Count; tier++)
                    {
                        stats[size][tier] = kind.StatsAt(composition, tier, sizeClass: size);

                        if (goldenStats != null)
                            goldenStats[size][tier] = kind.StatsAt(composition, tier, golden: true, sizeClass: size);
                    }
                }

                _rows.Add(kind, new Row(composition, stats, goldenStats));
                kinds[i] = kind;
            }

            if (compositions != null)
            {
                foreach (EnemyDefinition kind in compositions.Keys)
                {
                    if (kind == null || !_rows.ContainsKey(kind))
                        throw new ArgumentException($"이 판의 적 종류가 아니다: '{kind?.Id}'.", nameof(compositions));
                }
            }

            Kinds = Array.AsReadOnly(kinds);
            LinkKinds(kinds);
        }

        // 이 종류의 생성 중 변환 비율만큼 나오는 다음 종류. 없으면 null이다.
        public EnemyDefinition UpgradeTargetOf(EnemyDefinition kind)
        {
            Require(kind);
            return _upgradeTargets.TryGetValue(kind, out EnemyDefinition target) ? target : null;
        }

        // 이 종류로 정해진 생성 중 생성 확률만큼 대신 나오는 특수 종류(콘텐츠 순서). 없으면 비어 있다.
        public IReadOnlyList<EnemyDefinition> SpecialsOf(EnemyDefinition kind)
        {
            Require(kind);

            return _specials.TryGetValue(kind, out List<EnemyDefinition> specials)
                ? specials
                : (IReadOnlyList<EnemyDefinition>)Array.Empty<EnemyDefinition>();
        }

        private void LinkKinds(EnemyDefinition[] kinds)
        {
            var byId = new Dictionary<string, EnemyDefinition>(StringComparer.Ordinal);

            foreach (EnemyDefinition kind in kinds)
                byId[kind.Id] = kind;

            var chanceSums = new Dictionary<EnemyDefinition, float>();

            foreach (EnemyDefinition kind in kinds)
            {
                if (kind.UpgradesTo != null)
                    _upgradeTargets.Add(kind, Find(byId, kind.UpgradesTo, kind));

                if (kind.SpecialOf == null)
                    continue;

                EnemyDefinition parent = Find(byId, kind.SpecialOf, kind);

                if (!_specials.TryGetValue(parent, out List<EnemyDefinition> specials))
                    _specials.Add(parent, specials = new List<EnemyDefinition>());

                specials.Add(kind);
                float sum = (chanceSums.TryGetValue(parent, out float before) ? before : 0) + CompositionOf(kind).SpecialChance;
                chanceSums[parent] = sum;

                if (sum > 1 + 1e-4f)
                    throw new ArgumentException($"'{parent.Id}'의 특수 종류 생성 확률 합이 100%를 넘는다({sum * 100:0.##}%).");
            }
        }

        private static EnemyDefinition Find(Dictionary<string, EnemyDefinition> byId, string id, EnemyDefinition from)
        {
            if (!byId.TryGetValue(id, out EnemyDefinition kind))
                throw new ArgumentException($"'{from.Id}'가 가리키는 종류 '{id}'가 이 판에 없다.");

            return kind;
        }

        // 이 판에서 이 종류의 판 구성(질량 단계·황금 비율·황금 배율·공급·변환·특수 확률·크기 등급).
        public EnemyComposition CompositionOf(EnemyDefinition kind) => RowOf(kind).Composition;

        // 이 판에서 이 종류의 색 비율(색 등급 표 순서). 판을 시작할 때의 성장도로 고른 줄이다.
        public IReadOnlyList<float> TierRatiosOf(EnemyDefinition kind)
        {
            Require(kind);
            return kind.TierRatiosAt(Stage);
        }

        // 이 판에서 이 종류·색 등급·크기 등급(황금이면 황금)이 받는 수치.
        public EnemyStats Of(EnemyDefinition kind, int tier, bool golden = false, int sizeClass = 0)
        {
            Row row = RowOf(kind);

            if (sizeClass < 0 || sizeClass >= row.Stats.Length)
                throw new ArgumentOutOfRangeException(
                    nameof(sizeClass), $"이 판에서 '{kind.Id}'의 크기 등급은 0부터 {row.Stats.Length - 1}까지다. 받은 값: {sizeClass}.");

            if (tier < 0 || tier >= row.Stats[sizeClass].Length)
                throw new ArgumentOutOfRangeException(nameof(tier), $"'{kind.Id}'의 색 등급은 0부터 {row.Stats[sizeClass].Length - 1}까지다. 받은 값: {tier}.");

            if (!golden)
                return row.Stats[sizeClass][tier];

            if (row.GoldenStats == null)
                throw new ArgumentException($"'{kind.Id}'는 황금이 되지 않는다.", nameof(golden));

            return row.GoldenStats[sizeClass][tier];
        }

        // 이 판의 종류가 아니면 예외다.
        internal void Require(EnemyDefinition kind) => RowOf(kind);

        private Row RowOf(EnemyDefinition kind)
        {
            if (kind == null || !_rows.TryGetValue(kind, out Row row))
                throw new ArgumentException($"이 판의 적 종류가 아니다: '{kind?.Id}'.", nameof(kind));

            return row;
        }

        private readonly struct Row
        {
            public readonly EnemyComposition Composition;
            // [크기 등급][색 등급].
            public readonly EnemyStats[][] Stats;
            // 황금이 되지 않는 종류는 null이다.
            public readonly EnemyStats[][] GoldenStats;

            public Row(EnemyComposition composition, EnemyStats[][] stats, EnemyStats[][] goldenStats)
            {
                Composition = composition;
                Stats = stats;
                GoldenStats = goldenStats;
            }
        }
    }
}
