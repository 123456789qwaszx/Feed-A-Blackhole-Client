using System.Collections.Generic;

namespace BlackHole.Core
{
    // 한 판의 적 목록과 사망 절차. 적 시스템이 판 안에서 가진 상태는 여기에 모인다.
    // - 출현: 정의·색 등급·황금 여부·이 판의 수치·위치로 적을 만들고 번호를 준다. 수치는 판의 적 수치 표(EnemyStatTable)에서 온다.
    // - 이동: 살아 있는 적이 행동에 따라 움직인다.
    // - 피해: 이 목록에 살아 있는 적만 받는다. 처음 죽은 순간 목록에서 빠지고, 사망 기록을 한 번 남기고, 처치 수에 들고, 그 적의 Gold가 이 판의 합계에 든다.
    // - 파괴: 피해·HP 계산 없이 사망을 확정한다. 이 목록에 살아 있는 적만 죽고, 그 뒤는 피해로 죽을 때와 같다.
    // - 종류별 살아 있는 수: 출현 때 늘고 사망 때 준다.
    // - 정리: 판이 끝난 뒤 남은 적을 목록에서 치운다. 처치가 아니다 — 사망 기록도, 처치 수도, Gold도 없다.
    // HQ EXP는 World가 사망이 확정될 때 블랙홀에 넣는다. 사망 효과는 사망 기록이나 이 절차에 붙는다.
    internal sealed class EnemyRoster
    {
        private readonly List<Enemy> _alive = new List<Enemy>();
        private readonly Dictionary<EnemyDefinition, int> _aliveByKind = new Dictionary<EnemyDefinition, int>();
        private readonly Dictionary<EnemyDefinition, int> _killsByKind = new Dictionary<EnemyDefinition, int>();
        private readonly List<EnemyDefinition> _killOrder = new List<EnemyDefinition>();
        private readonly List<DeathRecord> _deaths = new List<DeathRecord>();
        private int _nextEnemyId = 1;
        private long _nextDeathSequence = 1;

        // 살아 있는 적(출현 순서). 죽은 적은 즉시 빠진다.
        public IReadOnlyList<Enemy> Alive { get; }
        // 마지막 진행 동안 확정된 사망. 다음 진행이 시작될 때 비운다.
        public IReadOnlyList<DeathRecord> Deaths { get; }
        // 이 판에서 확정된 사망의 Gold 합계. 진행 상태에는 판이 끝난 뒤 결산(GameSession.Settle)이 더한다.
        public long EarnedGold { get; private set; }

        public EnemyRoster()
        {
            Alive = _alive.AsReadOnly();
            Deaths = _deaths.AsReadOnly();
        }

        // 지금 살아 있는 이 종류의 적 수.
        public int CountAlive(EnemyDefinition kind) =>
            kind != null && _aliveByKind.TryGetValue(kind, out int count) ? count : 0;

        // 이 판에서 지금까지의 종류별 처치 수(처음 처치한 순서).
        public IReadOnlyList<EnemyKillCount> Kills()
        {
            var kills = new EnemyKillCount[_killOrder.Count];

            for (int i = 0; i < kills.Length; i++)
                kills[i] = new EnemyKillCount(_killOrder[i], _killsByKind[_killOrder[i]]);

            return System.Array.AsReadOnly(kills);
        }

        public Enemy Spawn(EnemyDefinition definition, int tier, bool golden, EnemyStats stats, Point2 position)
        {
            var enemy = new Enemy(
                new EnemyId(_nextEnemyId++),
                definition,
                tier,
                golden,
                stats,
                position);

            _alive.Add(enemy);
            _aliveByKind[definition] = CountAlive(definition) + 1;
            return enemy;
        }

        public void Move(float delta)
        {
            for (int i = 0; i < _alive.Count; i++)
                _alive[i].Move(delta);
        }

        // true는 이번 피해로 처음 죽었다는 뜻이다. 이 목록에 살아 있는 적이 아니면(다른 판의 적, 정리된 적) HP도 바꾸지 않는다.
        public bool DealDamage(Enemy enemy, Damage damage)
        {
            if (!Holds(enemy) || !enemy.ApplyDamage(damage))
                return false;

            RecordDeath(enemy);
            return true;
        }

        // true는 이번에 처음 죽었다는 뜻이다. 이미 죽었거나 이 목록에 없는 적(다른 판의 적, 정리된 적)은 그대로 둔다.
        public bool Destroy(Enemy enemy)
        {
            if (!Holds(enemy) || !enemy.Destroy())
                return false;

            RecordDeath(enemy);
            return true;
        }

        // 이 목록에 살아 있는 적인가. 피해와 파괴는 이것을 먼저 본 뒤에만 적을 바꾼다.
        private bool Holds(Enemy enemy) => enemy.IsAlive && _alive.Contains(enemy);

        // 막 죽은 적의 사망 절차: 사망 기록, Gold, 목록에서 제외, 종류별 살아 있는 수와 처치 수.
        // Gold는 흡수 연출을 기다리지 않고 지금 이 판의 합계에 든다(GAME_RULES 9절).
        private void RecordDeath(Enemy enemy)
        {
            _deaths.Add(new DeathRecord(_nextDeathSequence++, enemy));
            EarnedGold = checked(EarnedGold + enemy.Stats.Gold);

            if (_alive.Remove(enemy))
                _aliveByKind[enemy.Definition] = CountAlive(enemy.Definition) - 1;

            if (_killsByKind.TryGetValue(enemy.Definition, out int kills))
            {
                _killsByKind[enemy.Definition] = kills + 1;
            }
            else
            {
                _killsByKind.Add(enemy.Definition, 1);
                _killOrder.Add(enemy.Definition);
            }
        }

        // 남은 적을 모두 치운다. 처치가 아니다. 치운 수를 돌려준다.
        public int ClearAlive()
        {
            int cleared = _alive.Count;
            _alive.Clear();
            _aliveByKind.Clear();
            return cleared;
        }

        public void BeginAdvance() => _deaths.Clear();
    }
}
