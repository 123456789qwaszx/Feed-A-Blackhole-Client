using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 적 콘텐츠 전체의 규칙: 적 종류의 ID는 유일하다. 전체 개체 수 상한이 있고, 전투 시작 공급이 그 안이다.
    // EnemyContent 생성자(첫 오류로 생성 실패)와 EnemyContentLoader(경로별 진단 수집)가 함께 쓴다.
    // 개별 정의의 수치 규칙은 각 정의 생성자에 있다 — 여기서 다시 보지 않는다.
    // 정의 객체로 해석되는 참조(공급의 적)는 EnemyContentLoader가 이 색인으로 해석하며 진단한다.
    internal static class EnemyContentInvariants
    {
        // 공급되는 종류(소행성·행성·별)의 색 수: 빨주노초파보. 질량 규칙(MassRule)은 색 수와 무관하게 동작하지만 콘텐츠는 6색으로 맞춘다.
        public const int SuppliedTierCount = 6;

        // 공급되는 종류는 색이 SuppliedTierCount개다. 픽업은 색 수를 보지 않는다.
        public static void CheckTierCounts(IReadOnlyList<EnemyDefinition> enemies, ICollection<ContentDiagnostic> into)
        {
            foreach (EnemyDefinition kind in enemies)
            {
                if (kind != null && !kind.IsPickup && kind.Tiers.Count != SuppliedTierCount)
                    into.Add(new ContentDiagnostic($"Enemies[{kind.Id}].Tiers",
                        $"공급되는 종류는 색이 {SuppliedTierCount}개(빨주노초파보)여야 한다. 지금 {kind.Tiers.Count}개."));
            }
        }

        // 전체 개체 수 상한은 0 이상이고, 출현 배치가 있으면(적을 만들 수 있으면) 1 이상이다.
        public static void CheckMaxAlive(EnemyPlacementDefinition placement, int maxAliveEnemies, ICollection<ContentDiagnostic> into)
        {
            if (maxAliveEnemies < 0)
                into.Add(new ContentDiagnostic("MaxAliveEnemies", "0 이상이 필요하다."));
            else if (placement != null && maxAliveEnemies < 1)
                into.Add(new ContentDiagnostic("MaxAliveEnemies", "출현 배치가 있으면 전체 개체 수 상한(1 이상)이 필요하다."));
        }

        // 픽업 종류가 있으면 픽업 출현 띠가 있어야 하고, 일반 출현 띠에서 풀었을 때 띠가 되어야 한다(바깥 반지름이 0보다 크다).
        // 일반 띠가 없으면 픽업도 나오지 않으므로 풀어 보지 않는다. 픽업 종류가 없으면 픽업 띠는 쓰이지 않아 보지 않는다.
        public static void CheckPickupPlacement(
            IReadOnlyList<EnemyDefinition> enemies,
            EnemyPlacementDefinition placement,
            PickupPlacementDefinition pickupPlacement,
            ICollection<ContentDiagnostic> into)
        {
            bool hasPickup = false;

            foreach (EnemyDefinition kind in enemies)
            {
                if (kind != null && kind.IsPickup)
                    hasPickup = true;
            }

            if (!hasPickup)
                return;

            if (pickupPlacement == null)
            {
                into.Add(new ContentDiagnostic("PickupPlacement", "픽업 종류가 있으면 픽업 출현 배치가 필요하다."));
                return;
            }

            if (placement == null)
                return;

            try
            {
                pickupPlacement.Resolve(placement);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                into.Add(new ContentDiagnostic("PickupPlacement",
                    $"일반 출현 띠(바깥 반지름 {placement.MaxDistance})에서 풀면 띠가 되지 않는다: {ex.Message}"));
            }
        }

        // 전투 시작 공급의 합(과 업그레이드가 더할 수 있는 공급 수 extra)이 전체 개체 수 상한 안이다.
        // 전투 시작에는 살아 있는 적이 없으므로, 이 합이 상한을 넘으면 약속한 적이 매 판 시작부터 버려진다.
        // extra는 노드를 모두 산 경우의 더할 공급 수다(UpgradeContentCheck). 콘텐츠만 볼 때는 0이다.
        public static void CheckStartSupplyFits(
            IReadOnlyList<SupplyRequest> startSupply,
            long extra,
            int maxAliveEnemies,
            ICollection<ContentDiagnostic> into)
        {
            long worst = extra;

            foreach (SupplyRequest request in startSupply)
                worst += request.Count;

            if (worst > maxAliveEnemies)
                into.Add(new ContentDiagnostic("MaxAliveEnemies",
                    extra > 0
                        ? $"전투 시작 공급(공급 수 노드를 모두 산 경우)이 {worst}마리인데, 전체 개체 수 상한은 {maxAliveEnemies}마리다."
                        : $"전투 시작 공급이 {worst}마리인데, 전체 개체 수 상한은 {maxAliveEnemies}마리다."));
        }

        // 공급은 픽업을 가리킬 수 없다 — 픽업은 등장 주기마다 나온다.
        public static void CheckSupplyKinds(IReadOnlyList<SupplyRequest> supply, string section, ICollection<ContentDiagnostic> into)
        {
            for (int i = 0; i < supply.Count; i++)
            {
                if (supply[i].Enemy.IsPickup)
                    into.Add(new ContentDiagnostic($"{section}[{i}].Enemy", $"'{supply[i].Enemy.Id}'는 픽업이라 공급할 수 없다."));
            }
        }

        public static void CollectEnemies(
            IReadOnlyList<EnemyDefinition> enemies,
            ICollection<ContentDiagnostic> into,
            out Dictionary<string, EnemyDefinition> enemiesById)
        {
            enemiesById = Index(enemies, "Enemies", "적", e => e.Id, into);
        }

        // 종류 사이의 연결(BLACKHOLE_LEVEL_PLAN 4.3): 변환 대상은 콘텐츠에 있고 픽업이 아니어야 한다.
        // 변환 사슬은 제자리로 돌아오지 않는다(한 마리의 변환이 끝나야 한다).
        public static void CheckKindLinks(
            IReadOnlyList<EnemyDefinition> enemies,
            IReadOnlyDictionary<string, EnemyDefinition> enemiesById,
            ICollection<ContentDiagnostic> into)
        {
            foreach (EnemyDefinition kind in enemies)
            {
                if (kind == null)
                    continue;

                string at = $"Enemies[{kind.Id}]";

                if (kind.UpgradesTo != null)
                {
                    if (!enemiesById.TryGetValue(kind.UpgradesTo, out EnemyDefinition target))
                        into.Add(new ContentDiagnostic(at + ".UpgradesTo", $"정의되지 않은 적 ID '{kind.UpgradesTo}'."));
                    else if (target.IsPickup)
                        into.Add(new ContentDiagnostic(at + ".UpgradesTo", $"'{target.Id}'는 픽업이라 변환 대상이 될 수 없다."));
                }

                var seen = new HashSet<string>(StringComparer.Ordinal) { kind.Id };

                for (EnemyDefinition next = kind; next.UpgradesTo != null && enemiesById.TryGetValue(next.UpgradesTo, out next);)
                {
                    if (!seen.Add(next.Id))
                    {
                        into.Add(new ContentDiagnostic(at + ".UpgradesTo", $"변환 사슬이 '{next.Id}'에서 다시 돈다."));
                        break;
                    }
                }
            }
        }

        private static Dictionary<string, T> Index<T>(
            IReadOnlyList<T> items,
            string section,
            string label,
            Func<T, string> idOf,
            ICollection<ContentDiagnostic> into) where T : class
        {
            var byId = new Dictionary<string, T>(StringComparer.Ordinal);

            for (int i = 0; i < items.Count; i++)
            {
                T item = items[i];

                if (item == null)
                    into.Add(new ContentDiagnostic($"{section}[{i}]", $"{label} 정의가 null이다."));
                else if (byId.ContainsKey(idOf(item)))
                    into.Add(new ContentDiagnostic($"{section}[{i}]", $"{label} ID '{idOf(item)}'가 중복됐다."));
                else
                    byId.Add(idOf(item), item);
            }

            return byId;
        }
    }
}
