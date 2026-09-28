using System.Collections.Generic;

namespace BlackHole.Core
{
    // 한 판의 적 목록과 사망 절차.
    // - 출현: 정의·색 등급·황금 여부·이 판의 수치·위치로 적을 만들고 번호를 줌.
    // - 이동: 살아 있는 적이 행동에 따라 움직임.
    // - 피해: 이 목록에 살아 있는 적 체크, Hp0될 시, 사망 기록, 처치수 ++, 보상 Gold량 합.
    // - 파괴: 피해·HP 계산 없이 사망을 확정한다. 이 목록에 살아 있는 적만 죽고, 그 뒤는 피해로 죽을 때와 같다.
    // - 정리: 판이 끝난 뒤 남은 적을 목록에서 치운다. 처치가 아니다 — 사망 기록도, 처치 수도, Gold도 없다.
    internal sealed class EnemyRoster
    {
        private readonly List<Enemy> _alive = new();
        private readonly Dictionary<EnemyDefinition, int> _aliveByKind = new();
        private readonly Dictionary<EnemyDefinition, int> _killsByKind = new();
        private readonly List<EnemyDefinition> _killOrder = new();
        private readonly List<DeathRecord> _deaths = new();
        private int _nextEnemyId = 1;
        private long _nextDeathSequence = 1;

        // 살아 있는 적.
        public IReadOnlyList<Enemy> Alive { get; }

        // 마지막 진행 동안 확정된 사망. 다음 진행이 시작될 때 비운다.
        public IReadOnlyList<DeathRecord> Deaths { get; }

        // 이 판에서 확정된 사망의 Gold 합계. 진행 상태에는 판이 끝난 뒤 결산(GameSession.Settle)이 더함.
        public long EarnedGold { get; private set; }

        public EnemyRoster()
        {
            Alive = _alive.AsReadOnly();
            Deaths = _deaths.AsReadOnly();
        }

        // 지금 살아 있는 특정 종류의 적 수.
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

        // true는 이번 피해로 죽었다는 뜻.
        public bool DealDamage(Enemy enemy, Damage damage)
        {
            if (!Holds(enemy) || !enemy.ApplyDamage(damage))
                return false;

            RecordDeath(enemy);
            return true;
        }

        public bool Destroy(Enemy enemy)
        {
            if (!Holds(enemy) || !enemy.Destroy())
                return false;

            RecordDeath(enemy);
            return true;
        }

        // 이 목록에 살아 있는 적인가. 피해와 파괴는 이것을 먼저 본 뒤에만 적을 바꾼다.
        private bool Holds(Enemy enemy) => enemy.IsAlive && _alive.Contains(enemy);

        // 막 죽은 적의 사망 절차:
        // 사망 기록, Gold, 목록에서 제외, 종류별 살아 있는 수와 처치 수.
        //
        // Gold는 흡수 연출을 기다리지 않고 지금 이 판의 합계에 더함.
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
