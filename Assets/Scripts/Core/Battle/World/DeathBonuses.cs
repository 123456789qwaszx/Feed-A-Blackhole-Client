using System;

namespace BlackHole.Core
{
    // 사망이 확정된 순간의 보너스 판정.
    // - 황금 치명타: 황금 성질이면 치명타 확률로 (이 적의 Gold × 치명타 Gold 배율, 반올림)을 황금 치명타 Gold로 센다.
    //   판의 Gold 합계(World.EarnedGold)는 사망 Gold와 이것을 합친 값이다.
    // - 재생성: 같은 종류 하나를 생성 요청으로 넣는다(다음 공급 처리에서 나온다. 색·성질·크기·위치는 새로 정한다).
    // - 시간 추가: 성공 수를 모아 두고, 판이 Step 뒤에 가져가 제한 시간을 늘린다(TakeTimeBonuses).
    // 픽업은 재생성·시간 추가를 하지 않는다.
    internal sealed class DeathBonuses
    {
        private readonly EnemyStatTable _stats;
        private readonly EnemySupply _supply;
        private readonly BattleRandom _goldenCritRandom;
        private readonly BattleRandom _respawnRandom;
        private readonly BattleRandom _timeBonusRandom;
        // 아직 판(GameSession)이 가져가지 않은 시간 추가 성공 수.
        private int _timeBonuses;

        // 이 판의 황금 치명타 Gold 합계.
        internal long GoldenCritGold { get; private set; }

        internal DeathBonuses(int seed, EnemyStatTable stats, EnemySupply supply)
        {
            _stats = stats;
            _supply = supply;
            _goldenCritRandom = new BattleRandom(seed, RandomStream.GoldenCrit);
            _respawnRandom = new BattleRandom(seed, RandomStream.Respawn);
            _timeBonusRandom = new BattleRandom(seed, RandomStream.TimeBonus);
        }

        internal void Roll(Enemy dead)
        {
            if (dead.Trait?.Effect is GoldenDefinition golden && _goldenCritRandom.Roll(golden.CritChance))
            {
                long bonus = (long)Math.Round(dead.Stats.Gold * golden.CritRewardScale, MidpointRounding.AwayFromZero);

                if (bonus > 0)
                    GoldenCritGold = checked(GoldenCritGold + bonus);
            }

            EnemyDefinition kind = dead.Definition;

            if (kind.IsPickup)
                return;

            EnemyComposition composition = _stats.CompositionOf(kind);

            if (_respawnRandom.Roll(composition.RespawnChance))
                _supply.Request(new SupplyRequest(kind, 1));

            if (_timeBonusRandom.Roll(composition.TimeChance))
                _timeBonuses++;
        }

        // 모아 둔 시간 추가 성공 수를 넘기고 0으로 되돌린다.
        internal int TakeTimeBonuses()
        {
            int taken = _timeBonuses;
            _timeBonuses = 0;
            return taken;
        }
    }
}
