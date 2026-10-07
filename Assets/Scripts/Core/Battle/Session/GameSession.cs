using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 한 판의 흐름: 시작(Begin) -> 진행(Advance) -> 종료(End) -> 결산(Settle).
    // - 시작: 판 조립(SessionAssembler) 직후 한 번 부른다.
    // - 진행: 시간을 흘려 World를 한 Step씩 처리하고, 제한 시간(TimeLimitRule)을 늘리거나 종료를 판정.
    //   정지는 판이 모른다. 정지 중에는 BattleSystem이 Advance를 부르지 않는다.
    // - 종료: 이정표에 닿았거나 제한 시간이 다 됐을 때, 또는 End를 부를 때.
    // - 결산: 판이 번 Gold를 진행 상태(PlayerState)에 더하고 성장도를 올린다.
    public sealed class GameSession
    {
        private readonly TimeLimitRule _timeLimit;
        private readonly int _seed;
        private readonly PlayerState _progress;
        private readonly IReadOnlyList<SupplyRequest> _startSupply;

        // 결산할 Gold. 판이 끝나는 순간 정해지고 바뀌지 않는다(End).
        private long _settledGold;

        // 판이 끝났는가. 끝난 뒤에는 진행하지 않고, 결과·결산·정리만 할 수 있다.
        private bool _ended;

        // 결산을 마쳤는가. 같은 판을 두 번 결산하지 않는다.
        private bool _settled;

        public World World { get; }

        // 판이 끝났는가(이정표·제한 시간·End). 판은 끝나도 결과를 꺼낼 때까지 남아 있다.
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

        // 전투 시작 공급을 내보낸다. 판 조립 직후 한 번 부른다.
        public void Begin()
        {
            foreach (SupplyRequest request in _startSupply)
                World.RequestSpawn(request);

            World.ProcessSpawnRequests();
        }

        // 한 Step을 처리한 뒤 제한 시간을 늘리고 종료를 판정한다. 끝난 판은 진행하지 않는다.
        public SessionStepResult Advance(float delta)
        {
            DefinitionGuard.Delta(delta);

            if (_ended || delta == 0)
                return default;

            World.BeginAdvance();

            float step = _timeLimit.LimitStep(Elapsed, delta);
            int raised = World.Step(step);
            Elapsed += step;

            // 이정표에 닿았으면 남은 시간과 관계없이 이 Step에서 판이 끝난다. 시간 연장도, Level업 연출도 하지 않는다.
            if (World.Hq.ReachedMilestone)
            {
                End();
                return new SessionStepResult(0, true);
            }

            // 시간 연장은 종료 판정보다 먼저다.
            ExtendTimeLimit(raised);

            if (_timeLimit.HasExpired(Elapsed))
                End();

            return new SessionStepResult(raised, _ended);
        }

        // 판을 끝낸다. 이미 끝났으면(시간이 다 됐거나 이정표에 닿았으면) 결과를 그대로 둔다.
        // 결산할 Gold는 지금 정한다: 이정표로 끝났으면 진행 상태의 Gold를 이정표의 목표 잔액까지 채우는 차액(이 판에서 번 Gold는 버린다),
        // 아니면 이 판에서 번 Gold. 차액은 진행 상태의 Gold에 달려 있어 결산 뒤에 다시 계산하면 0이 되므로 끝나는 순간 고정한다.
        // 전투 중에는 진행 상태가 바뀌지 않으므로 지금의 Gold가 결산 때의 Gold와 같다.
        public void End()
        {
            if (_ended)
                return;

            _ended = true;

            Hq hq = World.Hq;
            _settledGold = hq.ReachedMilestone ? hq.Milestone.RewardFor(_progress.Gold) : World.EarnedGold;
        }

        // 끝난 판에 남은 적과 처리되지 않은 생성·파괴 요청·사망 효과를 치운다. 처치가 아니다.
        public void ClearRemainingEnemies()
        {
            RequireEnded();
            World.ClearRemainingEnemies();
        }

        // 끝난 판의 결과 스냅샷. 판을 버린 뒤에도 남는 기록이다.
        public BattleRawData CreateRawData()
        {
            RequireEnded();
            Hq hq = World.Hq;
            return new BattleRawData(_seed, Elapsed, World.Kills(), World.EarnedGold, hq.Level, hq.Exp,
                hq.Stage, hq.NextStage, hq.Milestone, _settledGold);
        }

        // 결산: 끝난 판의 Gold를 진행 상태에 더하고, 이 판이 이정표에 닿았으면 성장도를 1 올린다(Hq.NextStage).
        public void Settle()
        {
            RequireEnded();

            if (_settled)
                return;

            _progress.EarnGold(_settledGold);
            _progress.KeepGrowthStage(World.Hq.NextStage);
            _settled = true;
        }

        // 제한 시간 연장:
        // - 6. Growth: 오른 Level마다 블랙홀의 성장 시간,
        // - 파괴 때 시간 추가가 성공한 수마다 판 설정의 추가 시간(World가 사망 순간에 판정해 모아 둔다).
        private void ExtendTimeLimit(int raised)
        {
            float growth = raised * World.Hq.GrowthTime;
            float kills = World.TakeTimeBonuses() * _timeLimit.Definition.KillTimeBonus;
            _timeLimit.Extend(growth + kills);
        }

        private void RequireEnded()
        {
            if (!_ended)
                throw new InvalidOperationException("끝난 판에서만 할 수 있다.");
        }
    }
}
