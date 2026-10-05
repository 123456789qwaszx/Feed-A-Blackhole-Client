using System.Collections.Generic;
using BlackHole.Core;

namespace BlackHole.Authoring
{
    // 노드 업그레이드가 쓸 수 있는 수치 이름과 뜻. 데이터 시트의 UpgradeStats 탭(드롭다운의 원본)과 NodeUpgrades 탭 검사가 쓴다.
    // 게임은 모르는 이름을 조용히 무시하므로(UpgradeTable), 오타를 잡는 곳은 여기다.
    //
    // 이름은 가져가는 시스템의 것(BreakerUpgradeStats, HqUpgradeStats, EnemyUpgradeStats)을 그대로 쓴다.
    // 새 수치 이름을 Core에 더하면 여기에도 더한다.
    // 적 종류의 수치는 그 종류에 뜻이 있을 때만 낸다: 성질 확률은 그 종류의 성질마다(황금이면 배율도), 변환은 변환 대상이 있는 종류,
    // 등장 확률은 픽업, 공급 수·질량·크기는 픽업이 아닌 종류.
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
                (BreakerUpgradeStats.CritDamage, "Breaker 치명타 피해 보너스(1 = +100%). 기본값은 Skills 탭 breaker.crit-damage. +100%에서 +125%로 올리려면 Add 0.25."),
                (BreakerUpgradeStats.MoonDuration, "달 버프 중첩 하나의 지속 시간(초). 기본값은 Skills 탭 breaker.moon-duration."),
                (BreakerUpgradeStats.MoonSpeedBonus, "달 버프 중첩 하나의 공격 속도 보너스(0.2 = +20%). 기본값은 Skills 탭 breaker.moon-speed-bonus."),
                (BreakerUpgradeStats.MoonRadiusBonus, "달 버프 중첩 하나의 공격 범위(반지름) 보너스(0.1 = +10%). 기본값은 Skills 탭 breaker.moon-radius-bonus."),
                (BreakerUpgradeStats.CometDuration, "혜성 버프 중첩 하나의 지속 시간(초). 기본값은 Skills 탭 breaker.comet-duration."),
                (BreakerUpgradeStats.CometCritDamageBonus, "혜성 버프 중첩 하나의 치명타 피해 보너스 증가(0.5 = +50%). 기본값은 Skills 탭 breaker.comet-crit-damage-bonus."),
                (HqUpgradeStats.GrowthTime, "판 Level업마다 제한 시간에 더하는 초. 기본값 0."),
            };

            foreach (EnemyData enemy in enemies)
            {
                string id = enemy.Id;
                bool pickup = enemy.PickupPeriod > 0;

                if (!pickup)
                {
                    names.Add((EnemyUpgradeStats.Mass(id),
                        $"{id}의 질량(%). 기본값 100. 색 분포를 정한다(100%마다 다음 색, 최대 3색, 800%면 마지막 색만). Add만 쓴다(예: 50 = +50%)."));
                    names.Add((EnemyUpgradeStats.Size(id),
                        $"{id}의 크기. 기본값 1, 최대 {SizeRule.Max}. 크기 1부터 이 값까지 같은 몫으로 섞여 나온다(크기 k: HP·Gold·EXP k배, 반지름 1 + 0.5(k − 1)배). 한 노드 = 더하기 1."));
                    names.Add((EnemyUpgradeStats.StartSupply(id), $"전투 시작에 {id}를 더 공급하는 수. 기본값 0."));
                    names.Add((EnemyUpgradeStats.GrowthSupply(id), $"블랙홀 Level업마다 {id}를 더 공급하는 수. 기본값 0."));

                    foreach (EnemyTraitData trait in enemy.Traits)
                    {
                        names.Add((EnemyUpgradeStats.TraitChance(id, trait.Id), $"{id}에 '{trait.Id}' 성질이 붙는 확률(%). 기본값 0. 한 종류의 성질 확률 합은 100 이하."));

                        if (trait.Effect?.Kind == "Golden")
                            names.Add((EnemyUpgradeStats.TraitMultiplier(id, trait.Id), $"{id}의 '{trait.Id}' 성질 Gold 배율. 기본값은 EnemyTraits 탭 multiplier."));
                    }
                }
                else
                {
                    names.Add((EnemyUpgradeStats.Chance(id), $"픽업 {id}가 등장 주기({enemy.PickupPeriod}초)마다 나오는 확률(%). 기본값 0."));
                }

                if (!string.IsNullOrEmpty(enemy.UpgradesTo))
                    names.Add((EnemyUpgradeStats.Upgrade(id), $"{id}가 {enemy.UpgradesTo}로 나오는 비율(%). 기본값 0."));
            }

            return names;
        }
    }
}
