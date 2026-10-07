using System;
using System.Threading.Tasks;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    internal sealed class BattleSystem
    {
        private enum State { Idle, Starting, Running, ShuttingDown }

        private readonly GameContent _content;
        private readonly PlayerState _progress;
        private readonly EnemyView _enemyView;
        private readonly BreakerView _breakerView;
        private readonly DeathEffectView _deathEffectView;
        private readonly HqView _hqView;
        private readonly BattleCameraFit _cameraFit;
        private State _state = State.Idle;

        public GameSession Session { get; private set; }
        public BattleRawData LastRawData { get; private set; }

        public bool IsRunning => _state == State.Running;

        public BattleSystem(
            GameContent content,
            PlayerState progress,
            EnemyView enemyView,
            BreakerView breakerView,
            DeathEffectView deathEffectView,
            HqView hqView,
            BattleCameraFit cameraFit)
        {
            _content = content;
            _progress = progress;
            _enemyView = enemyView;
            _breakerView = breakerView;
            _deathEffectView = deathEffectView;
            _hqView = hqView;
            _cameraFit = cameraFit;
        }

        public bool TryStart(UpgradeTable upgrades)
        {
            if (_state != State.Idle)
                return false;

            _state = State.Starting;
            int seed = Environment.TickCount;

            // 1. 업그레이드에서 바뀐 수치 받기: 업그레이드 표로
            //    이 판의 Breaker 수치, 판 구성과 적 수치 표를 확정.
            try
            {
                Session = SessionAssembler.CreateBattle(_content, _progress, seed, upgrades);
            }
            catch
            {
                _state = State.Idle;
                throw;
            }

            // 2. 카메라를 이 판의 전장 배율에 맞춘다. 적·Breaker는 월드 크기라 그만큼 작아 보인다.
            if (_cameraFit != null)
                _cameraFit.SetFieldScale(Session.World.Hq.FieldScale);

            // 3. 적 소환 단계 진입.
            Session.Begin();
            _enemyView.Reset();

            // 시작 직후 스냅: 아직 지난 시간이 없으니 흔들림 연출 없이 위치만 맞춘다.
            _enemyView.Synchronize(Session.World, false, 0f);
            _breakerView.Reset();
            _deathEffectView.Reset();
            _hqView.Reset();

            _state = State.Running;
            return true;
        }

        // 전투 Step과 적·스킬·사망 효과·블랙홀 표현을 진행한다. 이번 Step에서 판이 끝났을 때만 true를 반환한다.
        public BattleStepResult Tick(float delta)
        {
            if (_state != State.Running)
                return new BattleStepResult(false, 0);

            bool wasRunning = Session.Phase == SessionPhase.Running;

            int raised = Session.Advance(delta);

            bool paused = Session.Phase == SessionPhase.Paused;
            _enemyView.Synchronize(Session.World, paused, delta);
            _breakerView.Synchronize(Session.World, paused, delta);
            _deathEffectView.Synchronize(Session.World, Session.Phase == SessionPhase.Paused, delta);
            _hqView.Synchronize(Session.World, delta);

            bool battleEnded = wasRunning && Session.Phase == SessionPhase.Ended;

            return new BattleStepResult(battleEnded, raised);
        }

        public void TogglePause()
        {
            if (_state == State.Running)
                Session.TogglePause();
        }

        public async Task<BattleRawData> TryEndAsync()
        {
            if (_state != State.Running)
                return null;

            _state = State.ShuttingDown;

            // 1. 종료 요청. 시간이 끝나 이미 끝난 판이면 기존 결과를 유지한다.
            Session.RequestEnd();

            // 2. 화면에서 관리하던 적의 수가 0. 남은 적은 처치가 아니라 정리.
            Session.ClearRemainingEnemies();

            // 3. 처치 집계와 번 Gold를 계산해 보관.
            LastRawData = Session.CreateRawData();

            // 4. 결산: 판이 번 Gold를 진행 상태에 한 번 더한다.
            Session.Settle();

            // 5. 화면의 연출 정리. 지운 객체는 프레임 끝에 사라지므로 한 프레임 기다린 뒤 확인한다.
            _enemyView.Reset();
            _breakerView.Reset();
            _deathEffectView.Reset();
            _hqView.Reset();
            await Awaitable.NextFrameAsync();

            // 6. 완전 초기화: 판을 버린다.
            Session = null;
            _state = State.Idle;

            return LastRawData;
        }

        // 전투 중도 포기
        // 이번 판의 결과를 PlayerState에 반영하지 않는다.
        // Session.Settle()을 호출하지 않는다.
        public async Task<bool> TryAbandonAsync()
        {
            if (_state != State.Running)
                return false;

            _state = State.ShuttingDown;

            Session.RequestEnd();
            Session.ClearRemainingEnemies();

            _enemyView.Reset();
            _breakerView.Reset();
            _deathEffectView.Reset();
            _hqView.Reset();

            await Awaitable.NextFrameAsync();

            // 전투 자체를 폐기
            Session = null;
            _state = State.Idle;

            return true;
        }
    }
}
