using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 검증된 적 콘텐츠: 적 종류, 출현 배치, 전체 개체 수 상한, 전투 시작 공급. 읽기 전용이며 여러 판이 함께 쓴다.
    //
    // 생성자 보장(구현 = EnemyContentInvariants와 아래 검사):
    // [1] 적 종류 ID가 유일하다. 변환 대상·부모 종류가 콘텐츠에 있고, 부모는 특수 종류가 아니며, 변환 사슬이 돌지 않는다.
    // [2] 전투 시작 공급이 있으면 출현 배치가 있다. 공급은 적을 정의 객체로 참조한다(EnemyContentLoader가 ID를 해석하며 진단한다).
    // [3] 출현 배치가 있으면 전체 개체 수 상한이 1 이상이고, 전투 시작 공급이 그 안이다.
    // 오류가 있는 콘텐츠의 경로별 보고는 EnemyContentLoader가 맡는다.
    public sealed class EnemyContent
    {
        private readonly Dictionary<string, EnemyDefinition> _enemiesById;

        public IReadOnlyList<EnemyDefinition> Enemies { get; }
        // 출현 위치. 공급이 없으면 null일 수 있다.
        public EnemyPlacementDefinition EnemyPlacement { get; }
        // 한 판에 동시에 살아 있을 수 있는 적의 전체 최대 수(성능 예산, SYSTEM_CATALOG S08). 넘는 생성 요청은 버린다.
        public int MaxAliveEnemies { get; }
        // 전투 시작 공급. 전투를 시작할 때 한 번 공급한다. 업그레이드가 더하는 공급 수는 판 조립이 더한다.
        public IReadOnlyList<SupplyRequest> StartSupply { get; }

        public EnemyContent(
            IReadOnlyList<EnemyDefinition> enemies,
            EnemyPlacementDefinition enemyPlacement,
            int maxAliveEnemies,
            IReadOnlyList<SupplyRequest> startSupply)
        {
            Enemies = Copy(enemies);
            EnemyPlacement = enemyPlacement;
            MaxAliveEnemies = maxAliveEnemies;
            StartSupply = Copy(startSupply);

            var diagnostics = new List<ContentDiagnostic>();
            EnemyContentInvariants.CollectEnemies(Enemies, diagnostics, out _enemiesById);
            EnemyContentInvariants.CheckKindLinks(Enemies, _enemiesById, diagnostics);

            if (StartSupply.Count > 0 && EnemyPlacement == null)
                diagnostics.Add(new ContentDiagnostic("EnemyPlacement", "공급이 있으면 출현 배치가 필요하다."));

            EnemyContentInvariants.CheckMaxAlive(EnemyPlacement, MaxAliveEnemies, diagnostics);
            EnemyContentInvariants.CheckStartSupplyFits(StartSupply, 0, MaxAliveEnemies, diagnostics);

            if (diagnostics.Count > 0)
                throw new ArgumentException(diagnostics[0].ToString());
        }

        public bool TryGetEnemy(string id, out EnemyDefinition enemy)
        {
            enemy = null;
            return id != null && _enemiesById.TryGetValue(id, out enemy);
        }

        // 호출자가 원본 목록을 나중에 바꿔도 따라 바뀌지 않게 한다.
        private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source)
        {
            if (source == null)
                return Array.Empty<T>();

            var copy = new T[source.Count];

            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = source[i];
            }

            return Array.AsReadOnly(copy);
        }
    }
}
