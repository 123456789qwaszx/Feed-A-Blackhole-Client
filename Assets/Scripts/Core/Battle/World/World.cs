using System.Collections.Generic;

namespace BlackHole.Core
{
    // 한 판 안에 있는 것들(적, 조준점과 Breaker, 사망 효과, 블랙홀, 이 판이 번 Gold)과 한 Step의 처리 순서.
    // 적의 생성은 EnemySupply, 사망 순간의 보너스는 DeathBonuses가 맡고, 여기서는 Step의 정해진 자리에서 부른다.
    // 판의 난수는 seed 하나에서 용도마다 스트림을 따로 만든다(BattleRandom). 한 용도의 비율을 바꿔도 다른 용도의 순서는 그대로다.
    public sealed class World
    {
        private readonly EnemyRoster _enemies = new EnemyRoster();
        private readonly EnemySupply _supply;
        private readonly DeathBonuses _deathBonuses;

        // 이 판의 Breaker. 콘텐츠에 Breaker가 없으면 null.
        public BreakerSkill Breaker { get; }
        // Breaker가 치는 조준점(규칙 평면). 지금은 조준 입력(AimInput)이 포인터 위치로 채운다. 없으면 null.
        public Point2? AimPoint { get; private set; }
        // 살아 있는 적(픽업 포함). 죽은 적은 즉시 빠진다.
        public IReadOnlyList<Enemy> Enemies => _enemies.Alive;
        // 마지막 진행 동안 들어간 피해와 확정된 사망(일어난 순서). 다음 진행이 시작될 때 비운다.
        // 화면은 Step이 끝난 뒤 이것을 읽어 피격·사망 연출을 낸다. 정지 중에는 비우지 않으므로 Sequence로 한 번씩만 읽는다.
        public IReadOnlyList<HitRecord> Hits => _enemies.Hits;
        public IReadOnlyList<DeathRecord> Deaths => _enemies.Deaths;
        // 사망 효과의 대기열과 마지막 진행 동안의 효과 기록(번개 이동, 폭발, 레이저).
        public DeathEffects DeathEffects { get; }
        // 이 판의 블랙홀. 사망이 확정되는 순간 그 적의 EXP가 들고, Step의 4 자리에서 Level이 오른다.
        public Hq Hq { get; }
        // 이 판에서 확정된 사망의 Gold 합계. 진행 상태에는 판이 끝난 뒤 결산(GameSession.Settle)이 더한다.
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

        // 생성 요청: 다음 공급 처리(Step의 6 자리, 전투 시작 공급은 GameSession.Begin) 때 나온다.
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

        // 적에게 피해를 주는 입구. 스킬과 사망 효과는 모두 여기로 피해를 준다. true는 이번 피해로 처음 죽었다는 뜻이다.
        // 이 판에 살아 있는 적이 아니면 아무것도 바꾸지 않는다. 처음 죽으면: EXP → 사망 효과 대기열(특수 적) → 사망 보너스.
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

        // 한 Step. 순서가 중요한 처리는 여기에 문장 순서대로 쓴다.
        // 1. Enemy Action: 살아 있는 적이 행동에 따라 움직인다.
        // 2. Passive Attack: Breaker가 조준점을 친다. 피해로 죽은 적은 그 순간 사망이 확정된다(DealDamage).
        // 3. Death Effect: 예고가 끝난 레이저를 쏜 뒤, 이번 Step에 죽은 특수 적의 성질 효과를 사망 순서대로 처리한다.
        //    효과로 죽은 적도 같은 Step의 사망이다. 레이저 별은 여기서 예고를 시작하고, 예고 시간이 지난 Step에 쏜다.
        // 4. HQ EXP / Level: 쌓인 EXP로 블랙홀의 Level을 올린다.
        //    이정표 앞 성장도의 판이 목표 Level에 닿았으면 여기서 멈춘다. 5·6·7을 하지 않고 판(GameSession)이 끝난다.
        // 5. Growth: 오른 Level마다 성장 공급을 요청한다. 시간 연장은 판(GameSession)이 종료 판정 전에 한다.
        // 6. Enemy Supply: 쌓인 생성 요청(성장 공급·재생성)을 처리한다.
        // 7. Pickup: 픽업의 등장 주기를 진행하고, 찬 주기마다 등장 확률로 픽업을 만든다.
        // Gold와 EXP는 따로 자리가 없다. 사망이 확정되는 순간 그 적의 값이 이 판의 합계와 블랙홀에 든다.
        // 같은 Step에서 사망이 생성보다 먼저고, 생성된 적은 다음 Step부터 움직이고 공격 대상이 된다. 오른 Level 수를 돌려준다.
        internal int Step(float delta)
        {
            _enemies.Move(delta);
            Breaker?.Advance(delta, this);
            DeathEffects.Resolve(this, delta);

            int raised = Hq.RaiseLevels();

            if (Hq.ReachedMilestone)
                return raised;

            _supply.RequestGrowth(raised);
            _supply.ProcessRequests();
            _supply.AdvancePickups(delta);
            return raised;
        }
    }
}
