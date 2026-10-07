using System.Collections.Generic;

namespace BlackHole.Core
{
    // 한 판 안에 있는 것들(적, 조준점과 Breaker, 사망 효과, 블랙홀, 이 판이 번 Gold)과 한 Step의 처리 순서.
    // 적의 생성은 EnemySupply, 사망 순간의 보너스는 DeathBonuses가 맡고, 여기서는 Step의 정해진 자리에서 부른다.
    // 판의 난수는 seed 하나에서 용도마다 스트림을 따로 만든다(BattleRandom). 한 용도의 비율을 바꿔도 다른 용도의 순서는 그대로다.
    public sealed class World
    {
        private readonly EnemyRoster _enemies = new();
        private readonly EnemySupply _supply;
        private readonly DeathBonuses _deathBonuses;

        public BreakerSkill Breaker { get; }
        public Point2? AimPoint { get; private set; }

        public IReadOnlyList<Enemy> Enemies => _enemies.Alive;

        // 마지막 진행 동안 들어간 피해와 확정된 사망(일어난 순서).
        public IReadOnlyList<HitRecord> Hits => _enemies.Hits;
        public IReadOnlyList<DeathRecord> Deaths => _enemies.Deaths;

        // 사망 효과의 대기열과 마지막 진행 동안의 효과 기록(번개 이동, 폭발, 레이저).
        public DeathEffects DeathEffects { get; }

        // 블랙홀.
        public Hq Hq { get; }

        public long EarnedGold => _enemies.EarnedGold;

        // 이 판의 종류별 판 구성과 (종류, 색 등급, 성질, 크기)별 수치. 판 조립 때 정해졌다.
        internal EnemyStatTable Stats { get; }

        internal World(
            int seed,
            EnemyStatTable stats,
            EnemyPlacementDefinition placement,
            PickupPlacementDefinition pickupPlacement,
            Hq hq,
            BreakerDefinition breaker,
            IReadOnlyList<SupplyRequest> growthSupply)
        {
            Stats = stats;
            Hq = hq;
            DeathEffects = new DeathEffects(seed);
            Breaker = breaker != null ? new BreakerSkill(breaker, new BattleRandom(seed, BattleRandom.CriticalStream)) : null;
            _supply = new EnemySupply(seed, _enemies, stats, placement, pickupPlacement, growthSupply, Breaker);
            _deathBonuses = new DeathBonuses(seed, _enemies, stats, _supply);
        }

        internal void SetAimPoint(Point2? aimPoint) => AimPoint = aimPoint;

        // 이 판에서 지금까지의 종류별 처치 수(처음 처치한 순서).
        internal IReadOnlyList<EnemyKillCount> Kills() => _enemies.Kills();

        // 생성 요청: 지금 바로 적을 만드는 메서드가 아니라, 나중에 생성할 요청만 큐에 넣는다
        internal void RequestSpawn(SupplyRequest request) => _supply.Request(request);

        internal void ProcessSpawnRequests() => _supply.ProcessRequests();

        // 판(GameSession)이 Step 뒤에 가져가는 시간 추가 성공 수. 가져가면 0이 된다.
        internal int TakeTimeBonuses() => _deathBonuses.TakeTimeBonuses();

        // 판이 끝난 뒤 남은 적과 처리되지 않은 생성 요청·사망 효과를 치운다. 처치가 아니다(사망 기록·처치 수·Gold 없음).
        internal void ClearRemainingEnemies()
        {
            _supply.Clear();
            DeathEffects.Clear();
            _enemies.ClearAlive();
        }

        internal bool DealDamage(Enemy enemy, Damage damage)
        {
            if (!_enemies.DealDamage(enemy, damage))
                return false;

            Hq.AddExp(enemy.Stats.Exp);
            DeathEffects.Enqueue(enemy);
            _deathBonuses.Roll(enemy);

            return true;
        }

        internal void BeginAdvance()
        {
            _enemies.BeginAdvance();
            DeathEffects.BeginAdvance();
            Breaker?.BeginAdvance();
        }

        internal int Step(float delta)
        {
            // 1. Enemy Action
            _enemies.Move(delta);

            // 2. Passive Attack: (DealDamage).
            Breaker?.Advance(delta, this);

            // 3. Death Effect: 이번 Step에 죽은 특수 적의 성질 효과를 사망 순서대로 처리.
            DeathEffects.Resolve(this, delta);

            // 4. HQ EXP / Level: 쌓인 EXP로 블랙홀의 Level 계산.
            int raised = Hq.RaiseLevels();

            if (Hq.ReachedMilestone)
                return raised;

            // 5. Growth: 오른 Level마다 성장 공급을 요청.
            _supply.RequestGrowth(raised);

            // 6. Enemy Supply: 쌓인 생성 요청(성장 공급·재생성)을 처리.
            _supply.ProcessRequests();

            // 7. Pickup: 픽업의 등장 판정 진행.
            _supply.AdvancePickups(delta);
            return raised;
        }
    }
}
