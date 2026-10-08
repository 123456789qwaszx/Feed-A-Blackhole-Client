namespace BlackHole.Core
{
    // 사망이 확정된 뒤 따라오는 효과 판정.
    internal sealed class DeathBonuses
    {
        private readonly EnemyStatTable _stats;
        private readonly EnemySupply _supply;
        private readonly BattleRandom _respawnRandom;
        private readonly BattleRandom _timeBonusRandom;

        private int _timeBonuses; // 아직 판(GameSession)이 가져가지 않은 시간 추가 성공 수.

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
