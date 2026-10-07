namespace BlackHole.Core
{
    // 사망이 확정된 뒤 따라오는 효과 판정. 처치 보상(Gold)은 사망 확정 때 DeathRewards가 정한다.
    // - 재생성: 같은 종류 하나를 생성 요청으로 넣는다(다음 공급 처리에서 나온다. 색·성질·크기·위치는 새로 정한다).
    // - 시간 추가: 성공 수를 모아 두고, 판이 Step 뒤에 가져가 제한 시간을 늘린다(TakeTimeBonuses).
    // 픽업은 재생성·시간 추가를 하지 않는다.
    internal sealed class DeathBonuses
    {
        private readonly EnemyStatTable _stats;
        private readonly EnemySupply _supply;
        private readonly BattleRandom _respawnRandom;
        private readonly BattleRandom _timeBonusRandom;
        // 아직 판(GameSession)이 가져가지 않은 시간 추가 성공 수.
        private int _timeBonuses;

        internal DeathBonuses(int seed, EnemyStatTable stats, EnemySupply supply)
        {
            _stats = stats;
            _supply = supply;
            _respawnRandom = new BattleRandom(seed, RandomStream.Respawn);
            _timeBonusRandom = new BattleRandom(seed, RandomStream.TimeBonus);
        }

        internal void Roll(Enemy dead)
        {
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
