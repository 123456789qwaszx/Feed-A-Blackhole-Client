using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BlackHole.Core;

namespace BlackHole.Unity
{
    // 전투의 시작과 끝의 순서를 책임지는 상위 오케스트레이터. 각 시스템은 여기서 불릴 때만 시작하고 정리한다.
    //
    // 시작 순서: (사운드 — 그 시스템이 붙으면 이 앞에) → 적·전투 시스템 시작.
    // 종료 순서: 적·전투 시스템 정리(결산 포함) → (사운드 정리 — 붙으면 이 뒤에).
    // 스킬은 판의 일부라 따로 준비·정리하지 않는다: 판 조립 때 생기고, 스킬 화면은 적·전투 시스템이 적 화면과 함께 정리한다.
    // 조준 입력(AimInput)은 시작·정리할 상태가 없다 — 매 프레임 진행 중인 판에 조준점을 넣을 뿐이다.
    //
    // 진행 상태(PlayerState: Gold, 성장도)를 가진다. 진행 상태는 방장의 것 하나다.
    // 진행 상태는 전투 밖에서만 바뀐다: 노드 구매(업그레이드 화면, 전투 중에는 살 수 없다)와
    // 판의 결산(GameSession.Settle — 적·전투 시스템의 정리 순서 안에서 한 번). 그래서 저장은 전투 밖(휴식 공간)에서만 하면 된다.
    // 시작·종료 요청의 성공 결과만 돌려준다. 화면 전환은 요청한 흐름이 결정한다.
    internal sealed class BattleOrchestrator
    {
        private readonly BattleSystem _battle;

        // 방장의 진행 상태(Gold, 성장도). 오케스트레이터를 만들 때 만들고, 전투 사이에 이어진다(저장은 없다).
        public PlayerState Progress { get; }
        // 시작 또는 종료 순서를 처리하는 중인가. 이 동안 들어온 요청은 무시한다.
        public bool Busy { get; private set; }
        public bool CanStart => !Busy && _battle.IsIdle;
        public bool CanEnd => !Busy && _battle.CanShutdown;

        public BattleOrchestrator(BattleSystem battle, PlayerId host)
        {
            _battle = battle;
            Progress = new PlayerState(host);
        }

        public async Task<bool> StartBattleAsync()
        {
            if (!CanStart)
                return false;

            Busy = true;

            try
            {
                // 전투마다 seed를 새로 정한다. 쓴 seed는 판과 원자료에 남는다.
                await _battle.StartAsync(Progress, Environment.TickCount);
                return true;
            }
            finally
            {
                Busy = false;
            }
        }

        public async Task<BattleRawData> EndBattleAsync()
        {
            if (!CanEnd)
                return null;

            Busy = true;

            try
            {
                return await _battle.ShutdownAsync();
            }
            finally
            {
                Busy = false;
            }
        }
    }
}
