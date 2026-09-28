using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 한 판의 상태(준비/진행/정지/종료), 경과 시간, 결과, 요청 허용 여부를 가진다.
    // 판 안의 대상과 한 단계의 처리 순서는 World가, 종료 판정은 TimeLimitRule이 가진다.
    // 재시작은 같은 객체의 부분 초기화가 아니라 새 조립이다(SessionAssembler).
    //
    // 수명: 조립(Preparing) → Begin(전투 시작 공급, Running) → 끝(Ended: 시간 종료, 종료 요청, 이정표)
    //      → 남은 적 정리(처치 아님) → 원자료 만들기 → 결산. 이 순서를 누가 언제 부를지는 판 바깥(오케스트레이터)이 정한다.
    //      이정표에 닿으면 남은 시간과 관계없이 그 Step에서 끝나고, 결산은 번 Gold 대신 이정표의 보상을 준다.
    // 진행 상태(Gold·성장도)를 바꾸는 것은 결산뿐이다. 전투 중에는 진행 상태가 바뀌지 않으므로 저장은 전투 밖에서만 하면 된다.
    public sealed class GameSession
    {
        // 이 판에 묶인 진행 상태(방장의 것). 결산이 번 Gold를 여기에 더한다.
        private readonly PlayerState _progress;
        private readonly IReadOnlyList<SupplyRequest> _startSupply;
        private bool _settled;

        public World World { get; }
        public TimeLimitRule TimeLimit { get; }
        // 이 판을 조립한 난수 seed. 같은 콘텐츠·산 노드·seed면 같은 판이 나온다.
        public int Seed { get; }
        public SessionPhase Phase { get; private set; } = SessionPhase.Preparing;
        public float Elapsed { get; private set; }
        public float Remaining => TimeLimit.Remaining(Elapsed);
        // 판이 끝나기 전에는 null이다.
        public SessionResult Result { get; private set; }
        // 이 판의 업그레이드 표. 판 조립 때 방장의 산 노드로 한 번 만들어졌고, 판이 끝날 때까지 같다.
        // 판 조립이 이 표로 모든 참가자의 Breaker 수치와 적 종류의 판 구성을 이미 계산했다. 판 중에 표를 다시 읽는 시스템은 없다(콘솔 표시뿐).
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

        // 그 참가자의 조준점을 바꾼다. 없으면 null. 호스트가 입력(지금은 마우스)을 읽어 프레임마다 넣는다.
        // 판의 단계와 관계없이 받는다. 스킬은 공격할 때의 조준점을 읽는다.
        public void SetAimPoint(PlayerId player, Point2? aimPoint) => World.PlayerOf(player).SetAimPoint(aimPoint);

        // 전투를 시작한다: 전투 시작 공급을 생성 요청으로 넣고 그 자리(0초)에서 공급 처리한 뒤 진행 단계로 들어간다.
        // 준비 단계에서 한 번만 부를 수 있다.
        public void Begin()
        {
            if (Phase != SessionPhase.Preparing)
                throw new InvalidOperationException($"준비 단계에서만 시작할 수 있다. 지금: {Phase}.");

            foreach (SupplyRequest request in _startSupply)
                World.RequestSpawn(request);

            World.ProcessSpawnRequests();
            Phase = SessionPhase.Running;
        }

        // 진행 중일 때만 시간이 흐른다. 제한 시간을 넘겨 진행하지 않는다.
        // 한 단계를 처리한 뒤 종료를 판정한다(GAME_RULES 13·14절). 끝난 판은 더 진행하지 않는다.
        public void Advance(float delta)
        {
            DefinitionGuard.Delta(delta);

            if (Phase != SessionPhase.Running || delta == 0)
                return;

            World.BeginAdvance();

            float step = TimeLimit.LimitStep(Elapsed, delta);
            int raised = World.Step(step);
            Elapsed += step;

            // 이정표에 닿았으면 남은 시간과 관계없이 이 Step에서 판이 끝난다. 시간 연장은 하지 않는다(GAME_RULES 11절).
            if (World.Hq.ReachedMilestone)
            {
                End();
                return;
            }

            // 6. Growth의 시간 연장: 오른 Level마다 이 판의 제한 시간을 늘린다. 종료 판정보다 먼저다(GAME_RULES 13·14절).
            TimeLimit.Extend(raised * World.Hq.GrowthTime);

            if (TimeLimit.HasExpired(Elapsed))
                End();
        }

        // 진행 ↔ 정지. 준비 중이거나 끝난 판에서는 아무 일도 없다.
        public void TogglePause()
        {
            if (Phase == SessionPhase.Running)
                Phase = SessionPhase.Paused;
            else if (Phase == SessionPhase.Paused)
                Phase = SessionPhase.Running;
        }

        // 판을 끝내라는 요청. 이미 끝난 판이면 최초 결과를 그대로 둔다.
        public void RequestEnd() => End();

        // 끝난 판에 남은 적과 처리되지 않은 생성·파괴 요청을 치운다. 처치가 아니다 — 사망 기록도, 처치 수도 없다(GAME_RULES 9절).
        // 치운 적의 수를 돌려준다.
        public int ClearRemainingEnemies()
        {
            RequireEnded();
            return World.ClearRemainingEnemies();
        }

        // 끝난 판의 원자료(조립 조건, 진행 시간, 종류별 처치 수, 번 Gold, 블랙홀의 도달 Level·EXP·성장도)를 만든다. 진행 상태는 바꾸지 않는다.
        public BattleRawData CreateRawData()
        {
            RequireEnded();
            Hq hq = World.Hq;
            return new BattleRawData(Seed, Result.PlayedSeconds, World.Kills(), World.EarnedGold, hq.Level, hq.Exp,
                hq.Stage, hq.NextStage, hq.Milestone, SettledGold);
        }

        // 결산을 마쳤는가.
        public bool IsSettled => _settled;

        // 결산이 더하는 Gold: 이정표에 닿아 끝난 판은 번 Gold 대신 이정표의 고정 보상, 아니면 번 Gold(GAME_RULES 11절).
        public long SettledGold => World.Hq.ReachedMilestone ? World.Hq.MilestoneReward : World.EarnedGold;

        // 결산: 끝난 판의 Gold(SettledGold)를 진행 상태(방장의 것)에 더하고, 이 판이 목표 Level에 닿았으면 성장도를 1 올린다(Hq.NextStage).
        // 이 판의 EXP·Level은 버린다. 한 판에 한 번만 하고, 다시 불러도 아무 일도 없다.
        // Gold를 더한 뒤에야 결산을 마친 것으로 기록한다. 더하기가 실패하면(Gold 넘침) 예외가 나가고 결산하지 않은 상태로 남는다.
        public void Settle()
        {
            RequireEnded();

            if (_settled)
                return;

            _progress.EarnGold(SettledGold);
            _progress.KeepGrowthStage(World.Hq.NextStage);
            _settled = true;
        }

        // 결과를 확정한다.
        private void End()
        {
            if (Phase == SessionPhase.Ended)
                return;

            Result = new SessionResult(Elapsed);
            Phase = SessionPhase.Ended;
        }

        private void RequireEnded()
        {
            if (Phase != SessionPhase.Ended)
                throw new InvalidOperationException($"끝난 판에서만 할 수 있다. 지금: {Phase}.");
        }
    }
}
