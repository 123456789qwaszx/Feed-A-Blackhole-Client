using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 한 판의 적 공급을 담당한다.
    // 생성 요청에 따라 일반 적을 만들고, 혜성 같은 주기 출현 적을 별도 주기로 생성한다.
    // 적 종류의 변환과 시작, 성장 공급량은 판 조립 때 이미 정해져 들어온다.
    internal sealed class EnemySupply
    {
        // 일반 공급 적의 성능 보호용 상한. 게임 규칙상의 최대 개체 수는 아니다.
        private const int SafetyMaxAlive = 1000;

        private readonly EnemyRoster _enemies;
        private readonly EnemyStatTable _stats;
        private readonly EnemyPlacementDefinition _placement;
        private readonly PeriodicSpawnPlacementDefinition _periodicSpawnPlacement;

        // Level업 한 번마다 요청할 적 종류와 수.
        private readonly IReadOnlyList<SupplyRequest> _growthSupply;

        // 이 성질의 현재 활성 수.
        // 살아 있는 적과 Breaker에 남아 있는 해당 버프 중첩을 함께 센다.
        private readonly Func<EnemyDefinition, EnemyTraitDefinition, int> _countActive;

        private readonly BattleRandom _placementRandom;
        private readonly BattleRandom _periodicSpawnPlacementRandom;
        private readonly BattleRandom _periodicSpawnRandom;
        private readonly BattleRandom _rainRandom;

        // 종류별 색 등급·성질·크기 비율을 판 전체에 걸쳐 맞추는 몫 선택기.
        private readonly Dictionary<EnemyDefinition, QuotaPicker> _tierPickers = new();
        private readonly Dictionary<EnemyDefinition, QuotaPicker> _traitPickers = new();
        private readonly Dictionary<EnemyDefinition, QuotaPicker> _sizePickers = new();

        // 주기 출현 종류마다 다음 출현 판정까지 누적된 시간.
        private readonly List<SpawnClock> _spawnClocks = new();

        // 일반 공급 적의 안전 상한 계산에서 제외할 픽업 종류.
        private readonly List<EnemyDefinition> _pickupKinds = new();

        // 다음 공급 처리 때 생성할 요청.
        private readonly List<SupplyRequest> _requests = new();

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
            Func<EnemyDefinition, EnemyTraitDefinition, int> countActive)
        {
            _enemies = enemies;
            _stats = stats;
            _placement = placement;
            _periodicSpawnPlacement = periodicSpawnPlacement;
            _growthSupply = growthSupply;
            _countActive = countActive;
            _placementRandom = new BattleRandom(seed, RandomStream.Placement);
            _periodicSpawnPlacementRandom = new BattleRandom(seed, RandomStream.PeriodicSpawnPlacement);
            _periodicSpawnRandom = new BattleRandom(seed, RandomStream.PeriodicSpawn);
            _rainRandom = new BattleRandom(seed, RandomStream.Rain);

            var tierRandom = new BattleRandom(seed, RandomStream.Tier);
            var traitRandom = new BattleRandom(seed, RandomStream.Trait);
            var sizeRandom = new BattleRandom(seed, RandomStream.Size);

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

        // 생성 요청: 이 종류를 몇 마리. 다음 공급 처리(ProcessRequests) 때 사용.
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
        // 뽑힌 성질의 동시 상한(MaxActive)이 찼으면 붙지 않는다(원작 "달 최대 개수"). 뽑은 몫은 그대로 쓴 것으로 친다.
        // 상한은 그 성질의 지금 수(_countActive)에 건다.
        private EnemyTraitDefinition TraitOf(EnemyDefinition kind)
        {
            if (!_traitPickers.TryGetValue(kind, out QuotaPicker picker))
                return null;

            int picked = picker.Pick();

            if (picked == 0)
                return null;

            EnemyTraitDefinition trait = _stats.CompositionOf(kind).Traits[picked - 1];
            return trait.MaxActive > 0 && _countActive(kind, trait) >= trait.MaxActive ? null : trait;
        }
    }
}
