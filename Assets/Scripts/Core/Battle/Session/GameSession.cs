using System.Collections.Generic;

namespace BlackHole.Core
{
    // 한 판의 흐름: 시작(Begin) -> 진행(Advance) -> 종료(End) -> 결산(Settle).
    // - 시작: 판 조립(SessionAssembler) 직후 한 번 부름.
    // - 진행: 시간을 흘려 World를 한 Step씩 처리하고, 제한 시간(TimeLimitRule)을 늘리거나 종료를 판정.
    // - 종료: 이정표에 닿았거나 제한 시간이 다 됐을 때, 또는 End를 부를 때.
    // - 결산: 판이 번 Gold를 진행 상태(PlayerState)에 더하고 성장도를 올린다.
    public sealed class GameSession
    {
        private readonly TimeLimitRule _timeLimit;
        private readonly int _seed;
        private readonly PlayerState _progress;
        private readonly IReadOnlyList<SupplyRequest> _startSupply;

        private long _settledGold; // 결산할 Gold.
        private bool _ended;       // 판 종료 여부
        private bool _settled;     // 결산 여부

        public World World { get; }

        public bool IsEnded => _ended;
        public float Elapsed { get; private set; }
        public float Remaining => _timeLimit.Remaining(Elapsed);

        internal GameSession(
            World world,
            TimeLimitRule timeLimit,
            int seed,
            PlayerState progress,
            IReadOnlyList<SupplyRequest> startSupply)
        {
            World = world;
            _timeLimit = timeLimit;
            _seed = seed;
            _progress = progress;
            _startSupply = startSupply;
        }

        // Breaker가 칠 조준점. 없으면 null(조준하지 않음).
        public void SetAimPoint(Point2? aimPoint) => World.SetAimPoint(aimPoint);

        public void Begin()
        {
            foreach (SupplyRequest request in _startSupply)
                World.RequestSpawn(request);

            World.ProcessSpawnRequests();
        }

        public AdvanceResult Advance(float delta)
        {
            DefinitionGuard.Delta(delta);

            if (_ended || delta == 0)
                return default;

            World.BeginAdvance();

            float step = _timeLimit.LimitStep(Elapsed, delta);
            int raised = World.Step(step);
            Elapsed += step;

            // 이정표에 닿았으면 남은 시간과 관계없이 이 Step에서 판 종료.
            if (World.Hq.ReachedMilestone)
            {
                End();
                return new AdvanceResult(0, true);
            }

            ExtendTimeLimit(raised);

            if (_timeLimit.HasExpired(Elapsed))
                End();

            return new AdvanceResult(raised, _ended);
        }

        public void End()
        {
            if (_ended)
                return;

            _ended = true;

            Hq hq = World.Hq;
            _settledGold = hq.ReachedMilestone ? hq.Milestone.RewardFor(_progress.Gold) : World.EarnedGold;
        }

        // 끝난 판에 남은 적과 처리되지 않은 생성 요청·사망 효과를 치운다. 처치가 아니다.
        public void ClearRemainingEnemies()
        {
            World.ClearRemainingEnemies();
        }

        // 끝난 판의 결과 스냅샷. 판을 버린 뒤에도 남는 기록이다.
        public BattleRawData CreateRawData()
        {
            Hq hq = World.Hq;
            return new BattleRawData(_seed, Elapsed, World.Kills(), World.EarnedGold, hq.Level, hq.Exp,
                hq.Stage, hq.NextStage, hq.Milestone, _settledGold);
        }

        // 결산: 끝난 판의 Gold를 진행 상태에 더하고, 이 판이 이정표에 닿았으면 성장도를 1 올린다(Hq.NextStage).
        public void Settle()
        {
            if (_settled)
                return;

            _progress.EarnGold(_settledGold);
            _progress.KeepGrowthStage(World.Hq.NextStage);
            _settled = true;
        }

        // - growth: 오른 Level마다 블랙홀의 성장 시간,
        // - 파괴 때 시간 추가가 성공한 수마다 판 설정의 추가 시간(World가 사망 순간에 판정해 모아 둔다).
        private void ExtendTimeLimit(int raised)
        {
            float growth = raised * World.Hq.GrowthTime;
            float kills = World.TakeTimeBonuses() * _timeLimit.Definition.KillTimeBonus;
            _timeLimit.Extend(growth + kills);
        }
    }
}
