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
    //   한 마리마다: 생성 여과(전체 상한) → 종류(변환 사슬 → 특수 종류) → 색 등급(그 종류의 색 비율) → 황금 여부(그 종류의 황금 비율)
    //   → 크기 등급(열린 크기 등급이 같은 몫) → 위치.
    //   색·황금·크기는 몫 방식(QuotaPicker)으로 정한다. 수치(Gold 포함)는 판의 적 수치 표에서 (종류, 색 등급, 황금, 크기 등급)의 값이다.
    // 같은 Step에서 사망이 생성보다 먼저다. 그래서 죽어서 비운 자리(전체 상한)에 같은 Step의 생성이 들어갈 수 있다.
    // 생성된 적은 다음 Step부터 움직이고 공격 대상이 된다. 처리되지 않은 요청은 판 정리가 버린다.
    //
    // 판의 난수는 seed 하나에서 용도마다 스트림을 따로 만든다(BattleRandom). 한 용도의 비율을 바꿔도 다른 용도의 순서는 그대로다
    // (예: 황금 비율을 바꿔도 색과 위치의 순서는 같다). 참가자마다의 스킬 난수는 BattlePlayer가 받는다.
    public sealed class World
    {
        private readonly EnemyRoster _enemies = new EnemyRoster();
        private readonly SpawnFilter _filter;
        private readonly EnemyPlacementDefinition _placement;
        private readonly BattleRandom _placementRandom;
        // 종류마다 색 등급과 황금 여부를 고르는 몫. 판 조립 때 만들고 판 동안 이어진다(공급이 여러 번이어도 비율이 판 전체에 걸쳐 맞는다).
        // 황금 몫은 황금 비율이 0보다 큰 종류에만 있고, 칸은 (보통, 황금) 둘이다.
        private readonly Dictionary<EnemyDefinition, QuotaPicker> _tierPickers = new Dictionary<EnemyDefinition, QuotaPicker>();
        private readonly Dictionary<EnemyDefinition, QuotaPicker> _goldenPickers = new Dictionary<EnemyDefinition, QuotaPicker>();
        // 크기 몫은 크기 노드로 크기 등급이 둘 이상 열린 종류에만 있고, 칸은 열린 크기 등급(0 ~ SizeLevel)이다.
        private readonly Dictionary<EnemyDefinition, QuotaPicker> _sizePickers = new Dictionary<EnemyDefinition, QuotaPicker>();
        // 종류마다 어떤 종류로 나오는가를 고르는 몫(BLACKHOLE_LEVEL_PLAN 4.3). 변환 몫은 칸이 (그대로, 변환 대상) 둘이고 변환 비율이 0보다 큰 종류에만,
        // 특수 몫은 칸이 (그대로, 특수 종류들…)이고 특수 종류의 생성 확률 합이 0보다 큰 부모에만 있다.
        private readonly Dictionary<EnemyDefinition, QuotaPicker> _upgradePickers = new Dictionary<EnemyDefinition, QuotaPicker>();
        private readonly Dictionary<EnemyDefinition, QuotaPicker> _specialPickers = new Dictionary<EnemyDefinition, QuotaPicker>();
        private readonly List<SupplyRequest> _spawnRequests = new List<SupplyRequest>();
        private readonly List<Enemy> _destroyRequests = new List<Enemy>();
        private readonly List<BattlePlayer> _players;

        // 판 안의 참가자(판 조립 때 받은 순서). 이 순서로 공격한다.
        public IReadOnlyList<BattlePlayer> Players { get; }
        // 살아 있는 적. 죽은 적은 즉시 빠진다.
        public IReadOnlyList<Enemy> Enemies => _enemies.Alive;
        // 마지막 진행 동안 확정된 사망. 다음 진행이 시작될 때 비운다.
        public IReadOnlyList<DeathRecord> Deaths => _enemies.Deaths;
        // 사망 효과의 대기열과 마지막 진행 동안의 효과 기록(번개 이동, 폭발).
        public DeathEffects DeathEffects { get; } = new DeathEffects();
        // 아직 처리되지 않은 생성 요청과 파괴 요청(들어온 순서).
        public IReadOnlyList<SupplyRequest> PendingSpawns { get; }
        public IReadOnlyList<Enemy> PendingDestroys { get; }
        // 이 판의 종류별 판 구성·색 비율과 (종류, 색 등급, 황금)별 수치. 판 조립 때 정해졌고 이 판 동안 바뀌지 않는다.
        public EnemyStatTable Stats { get; }
        // 한 판에 동시에 살아 있을 수 있는 적의 전체 최대 수. 이 수에 닿으면 생성 요청을 거른다(SpawnFilter).
        public int MaxAliveEnemies { get; }
        // 이 판의 블랙홀. 사망이 확정되는 순간 그 적의 EXP가 들고, Step의 5 자리에서 Level이 오른다.
        public Hq Hq { get; }

        internal World(
            int seed,
            EnemyStatTable stats,
            EnemyPlacementDefinition placement,
            int maxAliveEnemies,
            Hq hq,
            IReadOnlyList<BattlePlayer> players)
        {
            Stats = stats ?? throw new ArgumentNullException(nameof(stats));
            Hq = hq ?? throw new ArgumentNullException(nameof(hq));
            _placement = placement;
            _placementRandom = new BattleRandom(seed, BattleRandom.PlacementStream);
            _filter = new SpawnFilter(maxAliveEnemies);
            MaxAliveEnemies = maxAliveEnemies;
            _players = new List<BattlePlayer>(players);
            Players = _players.AsReadOnly();
            PendingSpawns = _spawnRequests.AsReadOnly();
            PendingDestroys = _destroyRequests.AsReadOnly();

            // 종류마다 처음 몫을 콘텐츠 순서로 흩뜨린다. 같은 콘텐츠·판 구성·seed면 같은 종류·색·황금 순서가 나온다.
            var tierRandom = new BattleRandom(seed, BattleRandom.TierStream);
            var goldenRandom = new BattleRandom(seed, BattleRandom.GoldenStream);
            var kindRandom = new BattleRandom(seed, BattleRandom.KindStream);
            var sizeRandom = new BattleRandom(seed, BattleRandom.SizeStream);

            foreach (EnemyDefinition kind in stats.Kinds)
            {
                _tierPickers.Add(kind, new QuotaPicker(stats.TierRatiosOf(kind), tierRandom));

                float golden = stats.CompositionOf(kind).GoldenRatio;

                if (golden > 0)
                    _goldenPickers.Add(kind, new QuotaPicker(new[] { 1 - golden, golden }, goldenRandom));

                int sizeLevel = stats.CompositionOf(kind).SizeLevel;

                if (sizeLevel > 0)
                {
                    var sizes = new float[sizeLevel + 1];

                    for (int i = 0; i < sizes.Length; i++)
                        sizes[i] = 1;

                    _sizePickers.Add(kind, new QuotaPicker(sizes, sizeRandom));
                }

                float upgrade = stats.CompositionOf(kind).UpgradeRatio;

                if (upgrade > 0)
                    _upgradePickers.Add(kind, new QuotaPicker(new[] { 1 - upgrade, upgrade }, kindRandom));

                IReadOnlyList<EnemyDefinition> specials = stats.SpecialsOf(kind);
                var chances = new float[specials.Count + 1];
                float chanceSum = 0;

                for (int i = 0; i < specials.Count; i++)
                {
                    chances[i + 1] = stats.CompositionOf(specials[i]).SpecialChance;
                    chanceSum += chances[i + 1];
                }

                if (chanceSum > 0)
                {
                    chances[0] = Math.Max(0, 1 - chanceSum);
                    _specialPickers.Add(kind, new QuotaPicker(chances, kindRandom));
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
        // 처리되므로 Step이 끝나면 대기열이 비어 있다. Step 밖에서 준 피해(DealDamage)로 죽은 효과 보유 적은 다음 Step까지 남는다.
        // 처리되지 않은 파괴 요청은 아직 사망이 아니다 — 판이 끝나면 처리되지 않고 판 정리가 버린다.
        public bool HasPendingDeathProcessing => DeathEffects.HasPending;

        internal IReadOnlyList<EnemyKillCount> Kills() => _enemies.Kills();

        // 생성 요청: 이 종류를 몇 마리. 다음 공급 처리(Step의 Enemy Supply 자리) 때 처리된다.
        // 이 판의 종류가 아니거나, 콘텐츠에 출현 배치가 없으면 요청 때 거부한다.
        // 전체 상한에 닿았으면 처리 때 생성 여과 장치가 거른다.
        public void RequestSpawn(SupplyRequest request)
        {
            Stats.Require(request.Enemy);

            if (_placement == null)
                throw new InvalidOperationException("이 판의 콘텐츠에는 출현 배치가 없어 적을 생성할 수 없다.");

            _spawnRequests.Add(request);
        }

        // 파괴 요청: 이 적의 사망을 확정하라. 다음 사망 처리(Step의 Damage / Death 자리) 때 처리된다.
        // 처리 때 이미 죽었거나 판에 없는 적의 요청은 아무것도 하지 않는다(같은 적의 두 번째 요청도 그렇다).
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
        // 여과를 통과한 한 마리마다: 어떤 종류로 나오는가(변환 사슬 → 특수 종류) → 색 등급 → 황금 → 위치.
        // 여과는 수만 보므로 종류·색·황금 때문에 걸러지는 일은 없고, 걸러진 요청은 몫을 쓰지 않는다.
        internal void ProcessSpawnRequests()
        {
            foreach (SupplyRequest request in _spawnRequests)
            {
                for (int i = 0; i < request.Count; i++)
                {
                    if (!_filter.Allows(_enemies))
                        continue;

                    EnemyDefinition kind = KindOf(request.Enemy);
                    int tier = _tierPickers[kind].Pick();
                    bool golden = _goldenPickers.TryGetValue(kind, out QuotaPicker goldenPicker) && goldenPicker.Pick() == 1;
                    int size = _sizePickers.TryGetValue(kind, out QuotaPicker sizePicker) ? sizePicker.Pick() : 0;
                    _enemies.Spawn(kind, tier, golden, Stats.Of(kind, tier, golden, size), _placement.Pick(_placementRandom));
                }
            }

            _spawnRequests.Clear();
        }

        // 요청한 종류에서 이 한 마리가 나올 종류: 변환 몫이 있으면 그 비율만큼 다음 종류로(사슬로 이어진다),
        // 그렇게 정해진 종류에 특수 몫이 있으면 그 확률만큼 특수 종류로.
        private EnemyDefinition KindOf(EnemyDefinition requested)
        {
            EnemyDefinition kind = requested;

            while (_upgradePickers.TryGetValue(kind, out QuotaPicker upgrade) && upgrade.Pick() == 1)
                kind = Stats.UpgradeTargetOf(kind);

            if (_specialPickers.TryGetValue(kind, out QuotaPicker special))
            {
                int picked = special.Pick();

                if (picked > 0)
                    kind = Stats.SpecialsOf(kind)[picked - 1];
            }

            return kind;
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
        // 4. Death Effect: 이번 Step에 피해로 죽은 효과 보유 적의 효과를 사망 순서대로 처리한다. 효과로 죽은 적도 같은 Step의 사망이다.
        // 5. HQ EXP / Level: 쌓인 EXP로 블랙홀의 Level을 올린다.
        //    이정표 앞 성장도의 판이 목표 Level에 닿았으면 여기서 멈춘다 — 6·7을 하지 않고, 판(GameSession)이 끝난다.
        // 6. Growth: 오른 Level마다 종류의 성장 공급을 생성 요청으로 넣는다. 시간 연장은 판(GameSession)이 종료 판정 전에 한다.
        // 7. Enemy Supply: 쌓인 생성 요청을 처리한다.
        // Gold와 EXP는 따로 자리가 없다 — 사망이 확정되는 순간 그 적에 이미 정해져 있던 값이 이 판의 합계와 블랙홀에 든다.
        // 오른 Level 수를 돌려준다.
        internal int Step(float delta)
        {
            _enemies.Move(delta);

            foreach (BattlePlayer player in _players)
                player.Attack(delta, this);

            ProcessDestroyRequests();
            DeathEffects.Resolve(this);

            int raised = Hq.RaiseLevels();

            // 이정표에 닿았으면 판이 이 Step에서 끝난다(GameSession). 성장 효과·공급은 하지 않는다(GAME_RULES 11절).
            if (Hq.ReachedMilestone)
                return raised;

            for (int i = 0; i < raised; i++)
                RequestGrowthSupply();

            ProcessSpawnRequests();
            return raised;
        }

        // Level업 한 번의 성장 공급: 종류마다 판 구성의 성장 공급 수만큼(콘텐츠 종류 순서). 나올 종류는 공급 처리가 변환·특수 확률로 정한다.
        private void RequestGrowthSupply()
        {
            foreach (EnemyDefinition kind in Stats.Kinds)
            {
                EnemyComposition composition = Stats.CompositionOf(kind);

                if (composition.GrowthSupply > 0)
                    RequestSpawn(new SupplyRequest(kind, composition.GrowthSupply));
            }
        }

        // 적에게 피해를 주는 입구. 피해를 주는 쪽(Skill·사망 효과)은 모두 여기로 요청한다.
        // 이 판에 살아 있는 적이 아니면(죽은 적, 전투 정리로 치운 적, 다른 판의 적) 아무것도 바꾸지 않는다 — HP·사망·Gold·사망 효과 모두.
        // true는 이번 피해로 처음 죽었다는 뜻이다.
        // 효과를 가진 적이 처음 죽으면 그 효과를 사망 효과 대기열에 넣는다(4. Death Effect 자리에서 처리).
        public bool DealDamage(Enemy enemy, Damage damage)
        {
            if (enemy == null)
                throw new ArgumentNullException(nameof(enemy));

            if (!_enemies.DealDamage(enemy, damage))
                return false;

            Hq.AddExp(enemy.Stats.Exp);
            DeathEffects.Enqueue(enemy, damage.Source);
            return true;
        }

        private void ProcessDestroyRequests()
        {
            foreach (Enemy enemy in _destroyRequests)
            {
                if (_enemies.Destroy(enemy))
                    Hq.AddExp(enemy.Stats.Exp);
            }

            _destroyRequests.Clear();
        }
    }
}
