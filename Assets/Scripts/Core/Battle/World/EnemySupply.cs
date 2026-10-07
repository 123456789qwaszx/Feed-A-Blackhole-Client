using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 한 판의 적 공급: 생성 요청을 받아 공급 처리 때 한 마리씩 만들고, 주기 출현 종류(혜성)를 출현 주기마다 만든다.
    // - 한 마리마다: 색 등급(종류의 색 비율) → 특수 성질(성질 확률, 최대 하나) → 크기(열린 크기가 같은 몫) → 위치.
    //   색·성질·크기는 몫 방식(QuotaPicker)으로 정한다. 수치(Gold 포함)는 판의 적 수치 표에서 (종류, 색 등급, 성질, 크기)의 값이다.
    // - 종류는 요청한 그대로다. 다음 종류로의 변환(소행성 → 행성)은 판 조립이 시작 공급을 정할 때 한 번 한다(GameSessionFactory).
    // - 전체 개체 수는 규칙으로 막지 않는다. 최후의 안전 상한(SafetyMaxAlive)에 닿았을 때만 남은 요청을 버린다.
    // - 주기 출현 종류(지금은 픽업인 혜성뿐)는 요청으로 나오지 않는다. 출현 주기마다 등장 확률로 하나를 주기 출현 띠 안에 만든다.
    internal sealed class EnemySupply
    {
        // 동시에 살아 있을 수 있는 공급된 적(픽업 제외)의 최후 안전 상한. 게임 규칙이 아니라 성능을 지키기 위한 보류다.
        private const int SafetyMaxAlive = 1000;

        private readonly EnemyRoster _enemies;
        private readonly EnemyStatTable _stats;
        private readonly EnemyPlacementDefinition _placement;
        // 주기 출현 띠: 일반 띠 바깥 반지름 기준 오프셋. 소환 때마다 그때의 일반 띠로 푼다.
        private readonly PeriodicSpawnPlacementDefinition _periodicSpawnPlacement;
        // Level업 한 번마다 넣는 생성 요청(판 조립이 이 판의 시작 수 × 성장 공급 %로 정했다).
        private readonly IReadOnlyList<SupplyRequest> _growthSupply;
        // 달 성질의 상한에 Breaker에 남은 달 중첩을 함께 센다. 콘텐츠에 Breaker가 없으면 null.
        private readonly BreakerSkill _breaker;
        private readonly BattleRandom _placementRandom;
        private readonly BattleRandom _periodicSpawnPlacementRandom;
        private readonly BattleRandom _periodicSpawnRandom;
        private readonly BattleRandom _rainRandom;
        // 종류마다 색 등급·성질·크기를 고르는 몫. 판 동안 이어진다(공급이 여러 번이어도 비율이 판 전체에 걸쳐 맞는다).
        // 성질 몫은 성질 확률 합이 0보다 큰 종류에만 있고, 칸은 (성질 없음, 성질 0, 성질 1, …)이다.
        // 크기 몫은 크기가 2 이상인 종류에만 있고, 칸 번호 + 1이 크기다.
        private readonly Dictionary<EnemyDefinition, QuotaPicker> _tierPickers = new Dictionary<EnemyDefinition, QuotaPicker>();
        private readonly Dictionary<EnemyDefinition, QuotaPicker> _traitPickers = new Dictionary<EnemyDefinition, QuotaPicker>();
        private readonly Dictionary<EnemyDefinition, QuotaPicker> _sizePickers = new Dictionary<EnemyDefinition, QuotaPicker>();
        // 주기 출현 종류마다 다음 출현 판정까지 지난 시간. 등장 확률이 0보다 큰 종류만 있다.
        private readonly List<SpawnClock> _spawnClocks = new List<SpawnClock>();
        // 픽업 종류. 공급된 적의 수(안전 상한)에서 뺀다.
        private readonly List<EnemyDefinition> _pickupKinds = new List<EnemyDefinition>();
        private readonly List<SupplyRequest> _requests = new List<SupplyRequest>();

        private sealed class SpawnClock
        {
            public EnemyDefinition Kind;
            public float Elapsed;
        }

        internal EnemySupply(
            int seed,
            EnemyRoster enemies,
            EnemyStatTable stats,
            EnemyPlacementDefinition placement,
            PeriodicSpawnPlacementDefinition periodicSpawnPlacement,
            IReadOnlyList<SupplyRequest> growthSupply,
            BreakerSkill breaker)
        {
            _enemies = enemies;
            _stats = stats;
            _placement = placement;
            _periodicSpawnPlacement = periodicSpawnPlacement;
            _growthSupply = growthSupply;
            _breaker = breaker;
            _placementRandom = new BattleRandom(seed, BattleRandom.PlacementStream);
            _periodicSpawnPlacementRandom = new BattleRandom(seed, BattleRandom.PeriodicSpawnPlacementStream);
            _periodicSpawnRandom = new BattleRandom(seed, BattleRandom.PeriodicSpawnStream);
            _rainRandom = new BattleRandom(seed, BattleRandom.RainStream);

            // 종류마다 처음 몫을 콘텐츠 순서로 흩뜨린다. 같은 콘텐츠·판 구성·seed면 같은 색·성질·크기 순서가 나온다.
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

                    if (composition.SpawnChance > 0)
                        _spawnClocks.Add(new SpawnClock { Kind = kind });

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

        // 생성 요청: 이 종류를 몇 마리. 다음 공급 처리(ProcessRequests) 때 나온다.
        internal void Request(SupplyRequest request) => _requests.Add(request);

        // Level업 한 번마다 성장 공급을 요청한다(판 조립이 정한 요청 그대로, 시작 공급 순서).
        internal void RequestGrowth(int levels)
        {
            for (int i = 0; i < levels; i++)
            {
                foreach (SupplyRequest request in _growthSupply)
                    _requests.Add(request);
            }
        }

        // 쌓인 생성 요청을 요청 순서대로, 요청한 수만큼 한 마리씩 출현 띠 안에 만든다.
        // 공급된 적이 안전 상한에 닿으면 남은 요청은 버린다. 버린 요청은 색·성질·크기 몫을 쓰지 않는다.
        internal void ProcessRequests()
        {
            // 공급 처리 중에는 죽는 적이 없으므로, 남은 자리는 처음에 한 번 세고 생성할 때마다 줄인다.
            int room = SafetyMaxAlive - SuppliedAlive();

            foreach (SupplyRequest request in _requests)
            {
                for (int i = 0; i < request.Count && room > 0; i++, room--)
                {
                    EnemyDefinition kind = request.Enemy;
                    int tier = _tierPickers[kind].Pick();
                    EnemyTraitDefinition trait = TraitOf(kind);
                    int size = _sizePickers.TryGetValue(kind, out QuotaPicker sizePicker) ? sizePicker.Pick() + SizeRule.Base : SizeRule.Base;
                    _enemies.Spawn(kind, tier, trait, size, _stats.Of(kind, tier, trait, size), _placement.Pick(_placementRandom));
                }
            }

            _requests.Clear();
        }

        // 주기 출현 종류마다 출현 주기가 찰 때마다 등장 확률로 하나를 주기 출현 띠 안에 만든다. 위치 난수는 일반 적과 따로다.
        // 혜성 비: 나오는 한 번이 혜성 비 확률로 종류의 혜성 비 수만큼이 된다. 픽업의 성질(혜성 버프)은 언제나 붙는다.
        internal void AdvancePeriodicSpawns(float delta)
        {
            foreach (SpawnClock clock in _spawnClocks)
            {
                clock.Elapsed += delta;
                EnemyDefinition kind = clock.Kind;
                EnemyComposition composition = _stats.CompositionOf(kind);

                while (clock.Elapsed >= kind.SpawnPeriod)
                {
                    clock.Elapsed -= kind.SpawnPeriod;

                    if (_periodicSpawnRandom.NextFloat() >= composition.SpawnChance)
                        continue;

                    int count = kind.RainCount > 1 && _rainRandom.Roll(composition.RainChance) ? kind.RainCount : 1;
                    EnemyTraitDefinition trait = composition.Traits[0];

                    for (int i = 0; i < count; i++)
                    {
                        int tier = _tierPickers[kind].Pick();
                        Point2 position = _periodicSpawnPlacement.Resolve(_placement).Pick(_periodicSpawnPlacementRandom);
                        _enemies.Spawn(kind, tier, trait, SizeRule.Base, _stats.Of(kind, tier, trait), position);
                    }
                }
            }
        }

        // 판 정리: 처리되지 않은 생성 요청을 버린다.
        internal void Clear() => _requests.Clear();

        // 공급된 적(픽업 제외)의 살아 있는 수. 안전 상한은 이 수에 건다.
        private int SuppliedAlive()
        {
            int count = _enemies.Alive.Count;

            for (int i = 0; i < _pickupKinds.Count; i++)
                count -= _enemies.CountAlive(_pickupKinds[i]);

            return count;
        }

        // 정해진 종류에 붙을 성질: 성질 몫이 있으면 그 확률만큼 성질 하나(배타). 없으면 null.
        // 뽑힌 성질의 동시 상한(MaxAlive)이 찼으면 붙지 않는다(원작 "달 최대 개수"). 뽑은 몫은 그대로 쓴 것으로 친다.
        // 상한에는 살아 있는 그 성질 적과, 그 성질이 준 버프 중 아직 Breaker에 남은 중첩(달)을 함께 센다.
        private EnemyTraitDefinition TraitOf(EnemyDefinition kind)
        {
            if (!_traitPickers.TryGetValue(kind, out QuotaPicker picker))
                return null;

            int picked = picker.Pick();

            if (picked == 0)
                return null;

            EnemyTraitDefinition trait = _stats.CompositionOf(kind).Traits[picked - 1];
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

        // 이 성질이 준 버프 중 아직 Breaker에 남은 중첩 수(달).
        private int HeldStacks(EnemyTraitDefinition trait) =>
            trait.Effect is MoonBuffDefinition && _breaker != null ? _breaker.MoonBuffs.Count : 0;
    }
}
