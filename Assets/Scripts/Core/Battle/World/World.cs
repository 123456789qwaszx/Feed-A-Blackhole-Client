using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 한 판 안에 존재하는 것들과 한 단계의 처리 순서.
    // 지금 판 안에 있는 것은 적, 참가자(조준점·스킬), 사망 효과 대기열, 블랙홀(EXP·Level), 이 판이 번 Gold다.
    // 각 시스템은 Step의 정해진 자리(GAME_RULES 13절)에 들어간다.
    //
    // 적이 생기고 죽는 일은 요청으로 들어와 쌓이고, Step의 정해진 자리에서 요청 순서대로 처리된다.
    // - 파괴 요청(RequestDestroy) → 13절 3. Damage / Death 자리: 그 적의 사망을 확정한다(피해·HP 계산 없음).
    // - 생성 요청(RequestSpawn)  → 13절 7. Enemy Supply 자리: 생성 여과 장치를 거쳐 한 마리씩 생성한다.
    //   한 마리마다: 생성 여과(전체 상한) → 색 등급(그 종류의 색 비율) → 특수 성질(그 종류의 성질 확률, 최대 하나)
    //   → 크기(열린 크기가 같은 몫) → 위치.
    //   색·성질·크기는 몫 방식(QuotaPicker)으로 정한다. 수치(Gold 포함)는 판의 적 수치 표에서 (종류, 색 등급, 성질, 크기)의 값이다.
    //   종류는 요청한 그대로다. 다음 종류로의 변환(소행성 → 행성)은 판 조립이 전투 시작 공급을 정할 때 한 번 한다(SessionAssembler).
    // - 픽업(혜성)은 요청으로 나오지 않는다 → 8. Pickup 자리: 종류의 등장 주기마다 등장 확률로 하나를 일반 띠와 다른 픽업 띠 안에 만든다(전체 상한과 무관).
    // 같은 Step에서 사망이 생성보다 먼저다. 그래서 죽어서 비운 자리(전체 상한)에 같은 Step의 생성이 들어갈 수 있다.
    // 생성된 적은 다음 Step부터 움직이고 공격 대상이 된다. 처리되지 않은 요청은 판 정리가 버린다.
    //
    // 판의 난수는 seed 하나에서 용도마다 스트림을 따로 만든다(BattleRandom). 한 용도의 비율을 바꿔도 다른 용도의 순서는 그대로다
    // (예: 성질 확률을 바꿔도 색과 위치의 순서는 같다). 참가자마다의 스킬 난수는 BattlePlayer가 받는다.
    public sealed class World
    {
        private readonly EnemyRoster _enemies = new EnemyRoster();
        private readonly SpawnFilter _filter;
        private readonly EnemyPlacementDefinition _placement;
        // 픽업(혜성)의 출현 띠: 일반 띠 바깥 반지름 기준 오프셋. 소환 때마다 그때의 일반 띠로 푼다.
        private readonly PickupPlacementDefinition _pickupPlacement;
        private readonly BattleRandom _placementRandom;
        private readonly BattleRandom _pickupPlacementRandom;
        private readonly BattleRandom _pickupRandom;
        // 파괴 때의 재생성·시간 추가, 혜성 비 판정. 용도마다 스트림이 따로다.
        private readonly BattleRandom _respawnRandom;
        private readonly BattleRandom _timeBonusRandom;
        private readonly BattleRandom _rainRandom;
        // 아직 판(GameSession)이 가져가지 않은 시간 추가 성공 수.
        private int _timeBonuses;
        // 황금 성질의 치명타 Gold 판정.
        private readonly BattleRandom _goldenCritRandom;
        // 종류마다 색 등급과 성질을 고르는 몫. 판 조립 때 만들고 판 동안 이어진다(공급이 여러 번이어도 비율이 판 전체에 걸쳐 맞는다).
        // 성질 몫은 성질 확률 합이 0보다 큰 종류에만 있고, 칸은 (성질 없음, 성질 0, 성질 1, …)이다.
        private readonly Dictionary<EnemyDefinition, QuotaPicker> _tierPickers = new Dictionary<EnemyDefinition, QuotaPicker>();
        private readonly Dictionary<EnemyDefinition, QuotaPicker> _traitPickers = new Dictionary<EnemyDefinition, QuotaPicker>();
        // 크기 몫은 크기가 2 이상인 종류에만 있고, 칸은 열린 크기(1 ~ Size)다. 칸 번호 + 1이 크기다.
        private readonly Dictionary<EnemyDefinition, QuotaPicker> _sizePickers = new Dictionary<EnemyDefinition, QuotaPicker>();
        // 픽업 종류마다 다음 등장 판정까지 지난 시간. 등장 확률이 0보다 큰 픽업만 있다.
        private readonly List<PickupClock> _pickupClocks = new List<PickupClock>();
        private readonly List<EnemyDefinition> _pickupKinds = new List<EnemyDefinition>();
        private readonly List<SupplyRequest> _spawnRequests = new List<SupplyRequest>();
        private readonly List<Enemy> _destroyRequests = new List<Enemy>();
        private readonly List<BattlePlayer> _players;

        private sealed class PickupClock
        {
            public EnemyDefinition Kind;
            public float Elapsed;
        }

        // 판 안의 참가자(판 조립 때 받은 순서). 이 순서로 공격한다.
        public IReadOnlyList<BattlePlayer> Players { get; }
        // 살아 있는 적(픽업 포함). 죽은 적은 즉시 빠진다.
        public IReadOnlyList<Enemy> Enemies => _enemies.Alive;
        // 마지막 진행 동안 확정된 사망. 다음 진행이 시작될 때 비운다.
        public IReadOnlyList<DeathRecord> Deaths => _enemies.Deaths;
        // 사망 효과의 대기열과 마지막 진행 동안의 효과 기록(번개 이동, 폭발, 레이저).
        public DeathEffects DeathEffects { get; }
        // 아직 처리되지 않은 생성 요청과 파괴 요청(들어온 순서).
        public IReadOnlyList<SupplyRequest> PendingSpawns { get; }
        public IReadOnlyList<Enemy> PendingDestroys { get; }
        // 이 판의 종류별 판 구성·색 비율과 (종류, 색 등급, 성질, 크기 등급)별 수치. 판 조립 때 정해졌고 이 판 동안 바뀌지 않는다.
        public EnemyStatTable Stats { get; }
        // 한 판에 동시에 살아 있을 수 있는 공급된 적(픽업 제외)의 전체 최대 수. 이 수에 닿으면 생성 요청을 거른다(SpawnFilter).
        public int MaxAliveEnemies { get; }
        // 블랙홀 Level업 한 번마다 넣는 생성 요청(판 조립이 이 판의 시작 수 × 성장 공급 %로 정했다). 판 동안 바뀌지 않는다.
        public IReadOnlyList<SupplyRequest> GrowthSupply { get; }
        // 이 판의 블랙홀. 사망이 확정되는 순간 그 적의 EXP가 들고, Step의 5 자리에서 Level이 오른다.
        public Hq Hq { get; }

        internal World(
            int seed,
            EnemyStatTable stats,
            EnemyPlacementDefinition placement,
            PickupPlacementDefinition pickupPlacement,
            int maxAliveEnemies,
            Hq hq,
            IReadOnlyList<BattlePlayer> players,
            IReadOnlyList<SupplyRequest> growthSupply = null)
        {
            Stats = stats ?? throw new ArgumentNullException(nameof(stats));
            Hq = hq ?? throw new ArgumentNullException(nameof(hq));
            _placement = placement;
            _pickupPlacement = pickupPlacement;
            _placementRandom = new BattleRandom(seed, BattleRandom.PlacementStream);
            _pickupPlacementRandom = new BattleRandom(seed, BattleRandom.PickupPlacementStream);
            _pickupRandom = new BattleRandom(seed, BattleRandom.PickupStream);
            _respawnRandom = new BattleRandom(seed, BattleRandom.RespawnStream);
            _timeBonusRandom = new BattleRandom(seed, BattleRandom.TimeBonusStream);
            _rainRandom = new BattleRandom(seed, BattleRandom.RainStream);
            _goldenCritRandom = new BattleRandom(seed, BattleRandom.GoldenCritStream);
            _filter = new SpawnFilter(maxAliveEnemies);
            MaxAliveEnemies = maxAliveEnemies;
            DeathEffects = new DeathEffects(seed);
            _players = new List<BattlePlayer>(players);
            Players = _players.AsReadOnly();
            PendingSpawns = _spawnRequests.AsReadOnly();
            PendingDestroys = _destroyRequests.AsReadOnly();
            GrowthSupply = GrowthSupplyOf(growthSupply, stats);

            // 종류마다 처음 몫을 콘텐츠 순서로 흩뜨린다. 같은 콘텐츠·판 구성·seed면 같은 종류·색·성질 순서가 나온다.
            var tierRandom = new BattleRandom(seed, BattleRandom.TierStream);
            var traitRandom = new BattleRandom(seed, BattleRandom.TraitStream);
            var sizeRandom = new BattleRandom(seed, BattleRandom.SizeStream);

            foreach (EnemyDefinition kind in stats.Kinds)
            {
                EnemyComposition composition = stats.CompositionOf(kind);
                _tierPickers.Add(kind, new QuotaPicker(stats.TierRatiosOf(kind), tierRandom));

                if (kind.IsPickup)
                {
                    _pickupKinds.Add(kind);

                    if (composition.AppearChance > 0)
                        _pickupClocks.Add(new PickupClock { Kind = kind });

                    continue;
                }

                float traitSum = composition.TraitChanceSum;

                if (traitSum > 0)
                {
                    var cells = new float[composition.Traits.Count + 1];
                    cells[0] = Math.Max(0, 1 - traitSum);

                    for (int i = 0; i < composition.Traits.Count; i++)
                        cells[i + 1] = composition.TraitChances[i];

                    _traitPickers.Add(kind, new QuotaPicker(cells, traitRandom));
                }

                int size = composition.Size;

                if (size > SizeRule.Base)
                {
                    var sizes = new float[size - SizeRule.Base + 1];

                    for (int i = 0; i < sizes.Length; i++)
                        sizes[i] = 1;

                    _sizePickers.Add(kind, new QuotaPicker(sizes, sizeRandom));
                }
            }
        }

        // 이 판의 참가자. 참가자가 아니면 예외다.
        public BattlePlayer PlayerOf(PlayerId id)
        {
            foreach (BattlePlayer player in _players)
            {
                if (player.Id.Equals(id))
                    return player;
            }

            throw new ArgumentException($"이 판의 참가자가 아니다: {id}.", nameof(id));
        }

        // 지금 살아 있는 이 종류의 적 수.
        public int CountAlive(EnemyDefinition kind) => _enemies.CountAlive(kind);

        // 이 판에서 지금까지 확정된 처치 수(모든 종류).
        public int TotalKills
        {
            get
            {
                int total = 0;

                foreach (EnemyKillCount kill in _enemies.Kills())
                    total += kill.Count;

                return total;
            }
        }

        // 이 판에서 확정된 사망의 Gold 합계. 사망 순간에 늘어난다.
        // 진행 상태(Gold)에는 판이 끝난 뒤 결산(GameSession.Settle)이 한 번 더한다 — 전투 중에는 진행 상태를 바꾸지 않는다.
        public long EarnedGold => _enemies.EarnedGold;

        // 확정된 사망 중 아직 처리가 끝나지 않은 것이 있는가. 판을 정리하기 전에 이것이 false여야 한다.
        // 사망 처리(목록에서 빠짐·사망 기록·처치 수·Gold 합계)는 사망 확정 순간에 끝난다. 사망 효과는 같은 Step의 4 자리에서
        // 처리되므로 Step이 끝나면 대기열이 비어 있다. Step 밖에서 준 피해(DealDamage)로 죽은 특수 적은 다음 Step까지 남는다.
        // 예외로 레이저 별의 레이저는 예고 시간 동안 남는다 — 판 정리(ClearRemainingEnemies)가 쏘지 않고 버린다.
        // 처리되지 않은 파괴 요청은 아직 사망이 아니다 — 판이 끝나면 처리되지 않고 판 정리가 버린다.
        public bool HasPendingDeathProcessing => DeathEffects.HasPending;

        internal IReadOnlyList<EnemyKillCount> Kills() => _enemies.Kills();

        // 생성 요청: 이 종류를 몇 마리. 다음 공급 처리(Step의 Enemy Supply 자리) 때 처리된다.
        // 이 판의 종류가 아니거나, 픽업이거나, 콘텐츠에 출현 배치가 없으면 요청 때 거부한다.
        // 전체 상한에 닿았으면 처리 때 생성 여과 장치가 거른다.
        public void RequestSpawn(SupplyRequest request)
        {
            Stats.Require(request.Enemy);

            if (request.Enemy.IsPickup)
                throw new ArgumentException($"'{request.Enemy.Id}'는 픽업이라 공급할 수 없다. 픽업은 등장 주기마다 나온다.", nameof(request));

            if (_placement == null)
                throw new InvalidOperationException("이 판의 콘텐츠에는 출현 배치가 없어 적을 생성할 수 없다.");

            _spawnRequests.Add(request);
        }

        // 파괴 요청: 이 적의 사망을 확정하라. 다음 사망 처리(Step의 Damage / Death 자리) 때 처리된다.
        // 처리 때 이미 죽었거나 판에 없는 적의 요청은 아무것도 하지 않는다(같은 적의 두 번째 요청도 그렇다).
        // [보류] 파괴로 죽은 특수 적의 성질 효과는 발동하지 않는다(ProcessDestroyRequests 참고).
        public void RequestDestroy(Enemy enemy)
        {
            _destroyRequests.Add(enemy ?? throw new ArgumentNullException(nameof(enemy)));
        }

        // 판이 끝난 뒤 남은 적과 처리되지 않은 요청·사망 효과를 치운다. 처치가 아니다(사망 기록·처치 수·Gold 없음). 치운 적의 수를 돌려준다.
        internal int ClearRemainingEnemies()
        {
            _spawnRequests.Clear();
            _destroyRequests.Clear();
            DeathEffects.Clear();
            return _enemies.ClearAlive();
        }

        // 공급 처리: 쌓인 생성 요청을 요청 순서대로, 한 마리씩 생성 여과 장치(전체 상한)를 거쳐 배치 띠 안에 생성한다.
        // 거른 요청은 버린다 — 나중에 자리가 나도 다시 나오지 않는다. 전투 시작 공급은 Begin(0초)이 바로 부른다.
        // 여과를 통과한 한 마리마다: 색 등급 → 성질 → 크기 → 위치. 종류는 요청한 그대로다.
        // 여과는 수만 보므로 종류·색·성질 때문에 걸러지는 일은 없고, 걸러진 요청은 몫을 쓰지 않는다.
        internal void ProcessSpawnRequests()
        {
            foreach (SupplyRequest request in _spawnRequests)
            {
                for (int i = 0; i < request.Count; i++)
                {
                    if (!_filter.Allows(SuppliedAlive))
                        continue;

                    EnemyDefinition kind = request.Enemy;
                    int tier = _tierPickers[kind].Pick();
                    EnemyTraitDefinition trait = TraitOf(kind);
                    int size = _sizePickers.TryGetValue(kind, out QuotaPicker sizePicker) ? sizePicker.Pick() + SizeRule.Base : SizeRule.Base;
                    _enemies.Spawn(kind, tier, trait, size, Stats.Of(kind, tier, trait, size), _placement.Pick(_placementRandom));
                }
            }

            _spawnRequests.Clear();
        }

        // 공급된 적(픽업 제외)의 살아 있는 수. 전체 상한은 이 수에 건다.
        private int SuppliedAlive
        {
            get
            {
                int count = _enemies.Alive.Count;

                for (int i = 0; i < _pickupKinds.Count; i++)
                    count -= _enemies.CountAlive(_pickupKinds[i]);

                return count;
            }
        }

        // 정해진 종류에 붙을 성질: 성질 몫이 있으면 그 확률만큼 성질 하나(배타). 없으면 null.
        // 뽑힌 성질의 동시 상한(MaxAlive)이 찼으면 붙지 않는다(원작 "달 최대 개수"). 뽑은 몫은 그대로 쓴 것으로 친다.
        // 상한에는 화면에 살아 있는 그 성질 적과, 그 성질이 준 버프 중 아직 Breaker에 남은 중첩(달)을 함께 센다.
        private EnemyTraitDefinition TraitOf(EnemyDefinition kind)
        {
            if (!_traitPickers.TryGetValue(kind, out QuotaPicker picker))
                return null;

            int picked = picker.Pick();

            if (picked == 0)
                return null;

            EnemyTraitDefinition trait = Stats.CompositionOf(kind).Traits[picked - 1];
            return trait.MaxAlive > 0 && CountAlive(kind, trait) + HeldStacks(trait) >= trait.MaxAlive ? null : trait;
        }

        // 이 종류 중 이 성질이 붙어 살아 있는 적의 수.
        private int CountAlive(EnemyDefinition kind, EnemyTraitDefinition trait)
        {
            int count = 0;
            IReadOnlyList<Enemy> alive = _enemies.Alive;

            for (int i = 0; i < alive.Count; i++)
            {
                if (alive[i].Definition == kind && ReferenceEquals(alive[i].Trait, trait))
                    count++;
            }

            return count;
        }

        // 이 성질이 준 버프 중 아직 Breaker에 남은 중첩 수(달). 버프는 모든 참가자가 함께 받으므로 가장 많이 가진 참가자의 수를 쓴다.
        private int HeldStacks(EnemyTraitDefinition trait)
        {
            if (!(trait.Effect is MoonBuffDefinition))
                return 0;

            int held = 0;

            foreach (BattlePlayer player in _players)
            {
                if (player.Breaker != null)
                    held = Math.Max(held, player.Breaker.MoonBuffs.Count);
            }

            return held;
        }

        // 픽업 처리: 픽업 종류마다 등장 주기가 찰 때마다 등장 확률로 하나를 픽업 전용 띠 안에 만든다. 전체 상한과 무관하다.
        // 픽업 띠는 일반 띠의 바깥 반지름 기준 오프셋이라 소환 때마다 그때의 일반 띠로 푼다. 위치 난수도 일반 적과 따로다.
        // 일반 띠나 픽업 띠가 없으면 나오지 않는다(콘텐츠 로더가 픽업 종류가 있으면 픽업 띠를 요구한다).
        // 픽업의 성질(혜성 버프)은 언제나 붙는다. 색은 그 종류의 색 비율이다.
        private void AdvancePickups(float delta)
        {
            if (_placement == null || _pickupPlacement == null)
                return;

            foreach (PickupClock clock in _pickupClocks)
            {
                clock.Elapsed += delta;
                EnemyDefinition kind = clock.Kind;
                EnemyComposition composition = Stats.CompositionOf(kind);

                while (clock.Elapsed >= kind.PickupPeriod)
                {
                    clock.Elapsed -= kind.PickupPeriod;

                    if (_pickupRandom.NextFloat() >= composition.AppearChance)
                        continue;

                    // 혜성 비: 나오는 한 번이 혜성 비 확률로 종류의 혜성 비 수만큼이 된다. 위치는 한 마리마다 따로 뽑는다.
                    int count = kind.PickupRainCount > 1 && Roll(composition.RainChance, _rainRandom) ? kind.PickupRainCount : 1;
                    EnemyTraitDefinition trait = composition.Traits[0];

                    for (int i = 0; i < count; i++)
                    {
                        int tier = _tierPickers[kind].Pick();
                        Point2 position = _pickupPlacement.Resolve(_placement).Pick(_pickupPlacementRandom);
                        _enemies.Spawn(kind, tier, trait, SizeRule.Base, Stats.Of(kind, tier, trait), position);
                    }
                }
            }
        }

        internal void BeginAdvance()
        {
            _enemies.BeginAdvance();
            DeathEffects.BeginAdvance();

            foreach (BattlePlayer player in _players)
                player.BeginAdvance();
        }

        // 한 단계. 순서가 중요한 처리는 여기에 문장 순서대로 쓴다(GAME_RULES 13절의 번호).
        // 1. Enemy Action: 살아 있는 적이 행동에 따라 움직인다.
        // 2. Passive Attack: 참가자 순서로 스킬이 공격한다. 피해로 죽은 적은 그 순간 사망이 확정된다(DealDamage).
        // 3. Damage / Death: 쌓인 파괴 요청의 사망을 확정한다.
        // 4. Death Effect: 예고가 끝난 레이저 별 레이저를 쏜 뒤, 이번 Step에 피해로 죽은 특수 적의 성질 효과를 사망 순서대로 처리한다.
        //    효과로 죽은 적도 같은 Step의 사망이다. 레이저 별은 여기서 예고를 시작하고, 예고 시간이 지난 Step에 쏜다.
        // 5. HQ EXP / Level: 쌓인 EXP로 블랙홀의 Level을 올린다.
        //    이정표 앞 성장도의 판이 목표 Level에 닿았으면 여기서 멈춘다 — 6·7·8을 하지 않고, 판(GameSession)이 끝난다.
        // 6. Growth: 오른 Level마다 성장 공급(GrowthSupply)을 생성 요청으로 넣는다. 시간 연장은 판(GameSession)이 종료 판정 전에 한다.
        // 7. Enemy Supply: 쌓인 생성 요청을 처리한다.
        // 8. Pickup: 픽업의 등장 주기를 진행하고, 찬 주기마다 등장 확률로 픽업을 만든다.
        // Gold와 EXP는 따로 자리가 없다 — 사망이 확정되는 순간 그 적에 이미 정해져 있던 값이 이 판의 합계와 블랙홀에 든다.
        // 오른 Level 수를 돌려준다.
        internal int Step(float delta)
        {
            _enemies.Move(delta);

            foreach (BattlePlayer player in _players)
                player.Attack(delta, this);

            ProcessDestroyRequests();
            DeathEffects.Resolve(this, delta);

            int raised = Hq.RaiseLevels();

            // 이정표에 닿았으면 판이 이 Step에서 끝난다(GameSession). 성장 효과·공급은 하지 않는다(GAME_RULES 11절).
            if (Hq.ReachedMilestone)
                return raised;

            for (int i = 0; i < raised; i++)
                RequestGrowthSupply();

            ProcessSpawnRequests();
            AdvancePickups(delta);
            return raised;
        }

        // Level업 한 번의 성장 공급: 판 조립이 정한 요청 그대로(시작 공급 순서). 색·성질·크기는 공급 처리가 정한다.
        private void RequestGrowthSupply()
        {
            foreach (SupplyRequest request in GrowthSupply)
                RequestSpawn(request);
        }

        // 성장 공급 요청은 이 판의 공급되는 종류여야 한다(픽업은 등장 주기로만 나온다).
        private static IReadOnlyList<SupplyRequest> GrowthSupplyOf(IReadOnlyList<SupplyRequest> growthSupply, EnemyStatTable stats)
        {
            var requests = new List<SupplyRequest>();

            if (growthSupply == null)
                return requests.AsReadOnly();

            foreach (SupplyRequest request in growthSupply)
            {
                stats.Require(request.Enemy);

                if (request.Enemy.IsPickup)
                    throw new ArgumentException($"'{request.Enemy.Id}'는 픽업이라 성장 공급에 둘 수 없다.", nameof(growthSupply));

                requests.Add(request);
            }

            return requests.AsReadOnly();
        }

        // 적에게 피해를 주는 입구. 피해를 주는 쪽(Skill·사망 효과)은 모두 여기로 요청한다.
        // 이 판에 살아 있는 적이 아니면(죽은 적, 전투 정리로 치운 적, 다른 판의 적) 아무것도 바꾸지 않는다 — HP·사망·Gold·사망 효과 모두.
        // true는 이번 피해로 처음 죽었다는 뜻이다.
        // 특수 적이 처음 죽으면 그 성질의 효과를 사망 효과 대기열에 넣는다(4. Death Effect 자리에서 처리).
        public bool DealDamage(Enemy enemy, Damage damage)
        {
            if (enemy == null)
                throw new ArgumentNullException(nameof(enemy));

            if (!_enemies.DealDamage(enemy, damage))
                return false;

            Hq.AddExp(enemy.Stats.Exp);
            DeathEffects.Enqueue(enemy, damage.Source);
            RollDeathBonuses(enemy);
            return true;
        }

        // 판(GameSession)이 Step 뒤에 가져가는 시간 추가 성공 수. 가져가면 0이 된다.
        internal int TakeTimeBonuses()
        {
            int taken = _timeBonuses;
            _timeBonuses = 0;
            return taken;
        }

        // 사망이 확정된 순간(피해·파괴 모두)의 판정. 픽업은 하지 않는다.
        // - 재생성: 같은 종류 하나를 생성 요청으로 넣는다(7. Enemy Supply 자리에서 전체 상한을 거쳐 나온다. 색·성질·크기·위치는 새로 정한다).
        // - 시간 추가: 성공 수를 모아 두고, 판이 Step 뒤에 제한 시간을 늘린다(TakeTimeBonuses).
        // - 황금 치명타: 황금 성질이면 치명타 확률로 (이 적의 Gold × 치명타 Gold 배율)을 판의 Gold 합계에 더 얹는다(반올림).
        private void RollDeathBonuses(Enemy enemy)
        {
            EnemyDefinition kind = enemy.Definition;

            if (enemy.Trait?.Effect is GoldenDefinition golden && Roll(golden.CritChance, _goldenCritRandom))
            {
                long bonus = (long)Math.Round(enemy.Stats.Gold * golden.CritRewardScale, MidpointRounding.AwayFromZero);

                if (bonus > 0)
                    _enemies.AddEarnedGold(bonus);
            }

            if (kind.IsPickup)
                return;

            EnemyComposition composition = Stats.CompositionOf(kind);

            if (_placement != null && Roll(composition.RespawnChance, _respawnRandom))
                _spawnRequests.Add(new SupplyRequest(kind, 1));

            if (Roll(composition.TimeChance, _timeBonusRandom))
                _timeBonuses++;
        }

        // 확률 판정. 0 이하·1 이상이면 굴리지 않는다(확률을 바꾸지 않은 판의 난수 순서가 그대로다).
        private static bool Roll(float chance, BattleRandom random)
        {
            if (chance <= 0)
                return false;

            return chance >= 1 || random.NextFloat() < chance;
        }

        // 파괴 요청의 사망 확정: Gold·EXP·사망 기록·처치 수는 피해로 죽을 때와 같다.
        // [보류] 사망 효과 대기열에는 넣지 않는다 — 파괴에는 피해의 출처(PlayerId)가 없어 효과 피해의 출처를 정할 수 없기 때문이다.
        // 그래서 지금은 파괴로 죽은 특수 적의 성질 효과(번개·폭발·버프 등)가 발동하지 않는다. "특수 효과는 사망 시 공통 발동" 규칙과 어긋난다.
        // 지금은 RequestDestroy를 부르는 곳이 없어 영향이 없다. 파괴 요청을 쓰게 되면 출처를 정하고 DeathEffects.Enqueue를 여기서 부른다.
        private void ProcessDestroyRequests()
        {
            foreach (Enemy enemy in _destroyRequests)
            {
                if (!_enemies.Destroy(enemy))
                    continue;

                Hq.AddExp(enemy.Stats.Exp);
                RollDeathBonuses(enemy);
            }

            _destroyRequests.Clear();
        }
    }
}
