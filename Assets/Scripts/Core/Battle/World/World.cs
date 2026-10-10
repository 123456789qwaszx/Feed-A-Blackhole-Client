using System.Collections.Generic;

namespace BlackHole.Core
{
    // 적, 조준점과 Breaker, 사망 효과, 블랙홀, 이 판이 번 Gold 등을 처리
    public sealed class World
    {
        private readonly EnemyRoster _enemies;
        private readonly EnemySupply _supply;
        private readonly DeathBonuses _deathBonuses;
        private readonly BattleStatsCounter _statsCounter = new();

        public BreakerSkill Breaker { get; }
        public Point2? AimPoint { get; private set; }

        public IReadOnlyList<Enemy> Enemies => _enemies.Alive;

        // 이번 Step의 피격과 사망(일어난 순서). 다음 Step이 시작될 때 비운다.
        public IReadOnlyList<HitRecord> Hits => _enemies.Hits;
        public IReadOnlyList<DeathRecord> Deaths => _enemies.Deaths;

        // 사망 효과의 대기열과 이번 Step의 효과 기록(번개 이동, 폭발, 레이저).
        public DeathEffects DeathEffects { get; }

        // 블랙홀.
        public Hq Hq { get; }

        // 이 판이 번 Gold(처치 보상 합계). 진행 상태에는 판이 끝난 뒤 결산(GameSession.Settle)이 더한다.
        public long EarnedGold => _enemies.KillGold;

        // 이 판의 종류별 판 구성과 (종류, 색 등급, 성질, 크기)별 수치. 판 조립 때 정해졌다.
        // 밖에서는 읽기만 한다(표는 읽기 API만 연다). 전투 통계가 이 판에 적용된 성질 확률·황금 배율을 읽는다.
        public EnemyStatTable Stats { get; }

        // 적 이동 배율. 게임에서는 늘 1이고, 테스트 도구(BattleCheats)만 바꾼다(0이면 멈춤).
        internal float EnemyMoveScale { get; set; } = 1f;

        internal World(
            int seed,
            EnemyStatTable stats,
            EnemyPlacementDefinition placement,
            PeriodicSpawnPlacementDefinition periodicSpawnPlacement,
            Hq hq,
            BreakerDefinition breaker,
            IReadOnlyList<SupplyRequest> growthSupply)
        {
            BattleRandom goldenCritRandom = new(seed, RandomStream.GoldenCrit);
            DeathRewards deathRewards = new(goldenCritRandom);
            _enemies = new EnemyRoster(deathRewards);
            Stats = stats;
            Hq = hq;
            DeathEffects = new DeathEffects(seed);

            BattleRandom criticalRandom = new(seed, RandomStream.Critical);
            Breaker = new BreakerSkill(breaker, criticalRandom);

            _supply = new EnemySupply(
                seed,
                _enemies,
                stats,
                placement,
                periodicSpawnPlacement,
                growthSupply,
                CountActive);

            _deathBonuses = new DeathBonuses(seed, stats, _supply);
        }

        internal void SetAimPoint(Point2? aimPoint) => AimPoint = aimPoint;

        // 이 판에서 지금까지의 종류별 처치 수(처음 처치한 순서).
        internal IReadOnlyList<EnemyKillCount> GetKillCounts() => _enemies.GetKillCounts();

        // 이 판에서 지금까지의 스킬별 피해와 수집 통계. 더해진 시간은 제한 시간을 가진 판(GameSession)이 넘긴다.
        internal BattleStats CreateStats(float addedSeconds) => _statsCounter.Snapshot(Breaker.TickCount, addedSeconds);

        // 생성 요청: 지금 바로 적을 만드는 메서드가 아니라, 나중에 생성할 요청만 큐에 넣는다
        internal void RequestSpawn(SupplyRequest request) => _supply.Request(request);

        internal void ProcessSpawnRequests() => _supply.ProcessRequests();

        // 판(GameSession)이 Step 뒤에 가져가는 시간 추가 성공 수. 가져가면 0이 된다.
        internal int TakeTimeBonuses() => _deathBonuses.TakeTimeBonuses();

        // 판이 끝난 뒤 남은 적과 처리되지 않은 생성 요청·사망 효과를 치운다.
        // 처치가 아니다(사망 기록·처치 수·Gold 없음).
        internal void ClearRemainingEnemies()
        {
            _supply.Clear();
            DeathEffects.Clear();
            _enemies.ClearAlive();
        }

        // 테스트 도구(BattleCheats)가 부른다. 정해진 적을 바로 만든다. 만든 수를 돌려준다.
        internal int SpawnExact(EnemyDefinition kind, int tier, EnemyTraitDefinition trait, int size, int count) =>
            _supply.SpawnExact(kind, tier, trait, size, count);

        // 테스트 도구(BattleCheats)가 부른다. 살아 있는 적만 치운다. 처치가 아니다(사망 기록·처치 수·Gold 없음).
        internal void ClearAliveEnemies() => _enemies.ClearAlive();

        internal bool DealDamage(Enemy enemy, Damage damage)
        {
            if (!_enemies.DealDamage(enemy, damage))
                return false;

            Hq.AddExp(enemy.Stats.Exp);
            DeathEffects.Enqueue(enemy);
            _deathBonuses.Roll(enemy);

            return true;
        }

        internal int Step(float delta)
        {
            // 0. 이번 Step의 기록(피격·사망, 사망 효과, Breaker Tick)을 비운다.
            _enemies.BeginStep();
            DeathEffects.BeginStep();
            Breaker.BeginStep();

            // 1. Enemy Action. 이동 배율 0(테스트 도구의 멈춤)이면 위치를 다시 계산하지 않는다 — 각도 재계산의 반올림으로 조금씩 밀리지 않게.
            if (EnemyMoveScale > 0)
                _enemies.Move(delta * EnemyMoveScale);

            // 2. Passive Attack: (DealDamage).
            Breaker.Advance(delta, this);

            // 3. Death Effect: 이번 Step에 죽은 특수 적의 성질 효과를 사망 순서대로 처리.
            DeathEffects.Resolve(this, delta);

            // 이번 Step의 피격·사망을 판 통계에 더한다. 이 뒤로 이번 Step에는 피해가 없다.
            _statsCounter.Count(_enemies.Hits, _enemies.Deaths);

            // 4. HQ EXP / Level: 쌓인 EXP로 블랙홀의 Level 계산.
            int raised = Hq.RaiseLevels();

            if (Hq.ReachedMilestone)
                return raised;

            // 5. Growth: 오른 Level마다 성장 공급을 요청.
            _supply.RequestGrowth(raised);

            // 6. Enemy Supply: 쌓인 생성 요청(성장 공급·재생성)을 처리.
            _supply.ProcessRequests();

            // 7. Periodic Spawn: 주기 출현(혜성)의 등장 판정 진행.
            _supply.AdvancePeriodicSpawns(delta);
            return raised;
        }

        // 이 성질이 지금 판에 있는 수: 살아 있는 그 성질 적 + 그 성질이 준 버프 중 Breaker에 남은 중첩(달).
        // 성질의 동시 상한(EnemyTraitDefinition.MaxActive, 원작 "달 최대 개수")은 이 수에 건다(EnemySupply).
        private int CountActive(EnemyDefinition kind, EnemyTraitDefinition trait)
        {
            int held = trait.Effect.Type == DeathEffectType.MoonBuff ? Breaker.MoonBuffs.Count : 0;
            return _enemies.CountAlive(kind, trait) + held;
        }
    }
}
