using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 한 판의 상태(준비/진행/정지/종료), 경과 시간, 결과, 요청 허용 여부를 가짐..
    // - World: 적 대상과 판정 처리 순서,
    // - TimeLimitRule: 종료 판정.
    //
    // 진행 상태(Gold·성장도)를 바꾸는 것은 결산뿐.
    // 전투 중에는 진행 상태가 바뀌지 않으므로 저장은 전투 밖에서만 하면 됨.
    public sealed class GameSession
    {
        // 이 판에 묶인 진행 상태. 결산이 번 Gold를 여기에 더함.
        private readonly PlayerState _progress;
        private readonly IReadOnlyList<SupplyRequest> _startSupply;
        private bool _settled;
        private long _settledGold;

        public World World { get; }

        public TimeLimitRule TimeLimit { get; }

        public int Seed { get; }

        public SessionPhase Phase { get; private set; } = SessionPhase.Preparing;

        public float Elapsed { get; private set; }

        public float Remaining => TimeLimit.Remaining(Elapsed);

        // 에디터용(판 조립이 이 표로 Breaker 수치와 적 종류의 판 구성을 이미 계산했음.)
        public UpgradeTable Upgrades { get; }

        internal GameSession(
            World world,
            TimeLimitRule timeLimit,
            int seed,
            PlayerState progress,
            UpgradeTable upgrades,
            IReadOnlyList<SupplyRequest> startSupply)
        {
            World = world;
            TimeLimit = timeLimit;
            Seed = seed;
            _progress = progress;
            Upgrades = upgrades;
            _startSupply = startSupply;
        }

        public void SetAimPoint(PlayerId player, Point2? aimPoint) =>
            World.PlayerOf(player).SetAimPoint(aimPoint);

        public void Begin()
        {
            if (Phase != SessionPhase.Preparing)
                throw new InvalidOperationException($"준비 단계에서만 시작할 수 있다. 지금: {Phase}.");

            foreach (SupplyRequest request in _startSupply)
                World.RequestSpawn(request);

            World.ProcessSpawnRequests();
            Phase = SessionPhase.Running;
        }

        // 진행 중일 때만 시간이 흐름.
        // 한 단계를 처리한 뒤 종료를 판정.
        public int Advance(float delta)
        {
            DefinitionGuard.Delta(delta);

            if (Phase != SessionPhase.Running || delta == 0)
                return 0;

            World.BeginAdvance();

            float step = TimeLimit.LimitStep(Elapsed, delta);
            int raised = World.Step(step);
            Elapsed += step;

            // 이정표에 닿았으면 남은 시간과 관계없이 이 Step에서 판이 끝난다. 시간 연장은 하지 않는다.
            if (World.Hq.ReachedMilestone)
            {
                End();
                return 0;
            }

            // 6. Growth의 시간 연장: 오른 Level마다 이 판의 제한 시간을 늘린다. 종료 판정보다 먼저다.
            TimeLimit.Extend(raised * World.Hq.GrowthTime);

            if (TimeLimit.HasExpired(Elapsed))
                End();
            
            return raised;
        }

        public void TogglePause()
        {
            if (Phase == SessionPhase.Running)
                Phase = SessionPhase.Paused;
            else if (Phase == SessionPhase.Paused)
                Phase = SessionPhase.Running;
        }

        public void RequestEnd() => End();

        // 끝난 판에 남은 적과 처리되지 않은 생성·파괴 요청을 치우는 용도
        public int ClearRemainingEnemies()
        {
            RequireEnded();
            return World.ClearRemainingEnemies();
        }

        // 전투 결과 스냅샷 생성
        public BattleRawData CreateRawData()
        {
            RequireEnded();
            Hq hq = World.Hq;
            return new BattleRawData(Seed, Elapsed, World.Kills(), World.EarnedGold, hq.Level, hq.Exp,
                hq.Stage, hq.NextStage, hq.Milestone, SettledGold);
        }

        public bool IsSettled => _settled;

        // 결산 때 더하는 Gold. 판이 끝나는 순간 정해지고 바뀌지 않는다(End).
        // - 이정표로 끝났으면 진행 상태의 Gold를 이정표의 목표 잔액까지 채우는 차액(이 판에서 번 Gold는 버린다),
        // - 아니면 이 판에서 번 Gold.
        // 차액은 진행 상태의 Gold에 달려 있어 결산 뒤에 다시 계산하면 0이 된다. 그래서 속성으로 계산하지 않고 끝날 때 고정한다.
        public long SettledGold
        {
            get
            {
                RequireEnded();
                return _settledGold;
            }
        }

        // 결산:
        // - 끝난 판의 Gold(SettledGold)를 진행 상태(방장의 것)에 더하고,
        // - 이 판이 이정표에 닿았으면 성장도를 1 올린다(Hq.NextStage).
        public void Settle()
        {
            RequireEnded();

            if (_settled)
                return;

            _progress.EarnGold(SettledGold);
            _progress.KeepGrowthStage(World.Hq.NextStage);
            _settled = true;
        }

        private void End()
        {
            if (Phase == SessionPhase.Ended)
                return;

            Phase = SessionPhase.Ended;

            // 전투 중에는 진행 상태가 바뀌지 않으므로 지금의 Gold가 결산 때의 Gold와 같다.
            Hq hq = World.Hq;
            _settledGold = hq.ReachedMilestone ? hq.Milestone.RewardFor(_progress.Gold) : World.EarnedGold;
        }

        private void RequireEnded()
        {
            if (Phase != SessionPhase.Ended)
                throw new InvalidOperationException($"끝난 판에서만 할 수 있다. 지금: {Phase}.");
        }
    }
}
