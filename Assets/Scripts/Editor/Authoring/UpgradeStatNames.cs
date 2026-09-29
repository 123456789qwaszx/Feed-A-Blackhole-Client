using System.Collections.Generic;
using BlackHole.Core;

namespace BlackHole.Authoring
{
    // 노드 업그레이드가 쓸 수 있는 수치 이름과 뜻. 데이터 시트의 UpgradeStats 탭(드롭다운의 원본)과 NodeUpgrades 탭 검사가 쓴다.
    // 게임은 모르는 이름을 조용히 무시하므로(UpgradeTable), 오타를 잡는 곳은 여기다.
    //
    // 이름은 가져가는 시스템의 것(BreakerUpgradeStats, HqUpgradeStats, EnemyUpgradeStats)을 그대로 쓴다.
    // 새 수치 이름을 Core에 더하면 여기에도 더한다.
    // 적 종류의 수치는 그 종류에 뜻이 있을 때만 낸다: 황금은 황금이 되는 종류, 변환은 변환 대상이 있는 종류, 생성 확률은 특수 종류.
    public static class UpgradeStatNames
    {
        public static List<(string Name, string Note)> For(IReadOnlyList<EnemyData> enemies)
        {
            var names = new List<(string, string)>
            {
                (BreakerUpgradeStats.Damage, "Breaker 피해. 기본값은 Skills 탭 breaker.damage."),
                (BreakerUpgradeStats.Speed, "Breaker 공격 속도. 기본값 1, 주기 = 기본 주기 ÷ 속도."),
                (BreakerUpgradeStats.Radius, "Breaker 공격 원의 반지름. 기본값은 Skills 탭 breaker.radius."),
                (BreakerUpgradeStats.CritChance, "Breaker 치명타 확률(최대 1). 기본값은 Skills 탭 breaker.crit-chance."),
                (HqUpgradeStats.GrowthTime, "판 Level업마다 제한 시간에 더하는 초. 기본값 0."),
            };

            foreach (EnemyData enemy in enemies)
            {
                string id = enemy.Id;
                names.Add((EnemyUpgradeStats.MassLevel(id), $"{id}의 질량 단계(HP·Gold 계수 줄). 기본값 0, 한 노드 = 더하기 1."));

                if (enemy.GoldenMultiplier > 0)
                {
                    names.Add((EnemyUpgradeStats.GoldenRatio(id), $"{id}가 황금으로 나오는 비율(0 ~ 1). 기본값 0."));
                    names.Add((EnemyUpgradeStats.GoldenMultiplier(id), $"{id}가 황금일 때 Gold 배율. 기본값은 Enemies 탭 goldenMultiplier."));
                }

                names.Add((EnemyUpgradeStats.StartSupply(id), $"전투 시작에 {id}를 더 공급하는 수. 기본값 0."));
                names.Add((EnemyUpgradeStats.GrowthSupply(id), $"블랙홀 Level업마다 {id}를 더 공급하는 수. 기본값 0."));

                if (!string.IsNullOrEmpty(enemy.UpgradesTo))
                    names.Add((EnemyUpgradeStats.Upgrade(id), $"{id}가 {enemy.UpgradesTo}로 나오는 비율(%). 기본값은 Enemies 탭 baseUpgrade."));

                if (!string.IsNullOrEmpty(enemy.SpecialOf))
                    names.Add((EnemyUpgradeStats.Chance(id), $"{enemy.SpecialOf} 대신 {id}가 나오는 확률(%). 기본값 0."));
            }

            return names;
        }
    }
}
