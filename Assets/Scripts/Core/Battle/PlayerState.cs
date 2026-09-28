using System;

namespace BlackHole.Core
{
    // Player 한 명의 진행 상태: Gold, 블랙홀의 성장도. 전투 사이에 유지된다(앱 종료 후 저장은 하지 않는다).
    // 새 진행은 새 PlayerState로 시작한다. 판은 PlayerState 목록을 받는다 — 지금 1명일 뿐 하나로 고정된 것이 아니다.
    public sealed class PlayerState
    {
        public PlayerId Id { get; }
        // 원작의 금액은 T(조) 단위까지 오르므로 int(약 21억)가 아니라 long이다.
        public long Gold { get; private set; }
        // 블랙홀의 성장도. 새 진행은 0이다. 결산 때만, 한 판에 최대 1 오른다. 줄지 않는다(BATTLE_COMPOSITION_PLAN 8절).
        // 판의 Level·EXP는 저장하지 않는다 — 매 판 0에서 시작한다. 이정표 진행도는 성장도로 계산한다.
        public int GrowthStage { get; private set; } = HqGrowthDefinition.StartStage;

        public PlayerState(PlayerId id)
        {
            Id = id;
        }

        // Gold를 더한다. 전투 중에는 부르지 않는다 — 판이 끝난 뒤 결산(GameSession.Settle)이 그 판이 번 Gold로 한 번 부른다.
        // 그래서 진행 상태는 전투 밖에서만 바뀌고, 저장 시점도 전투 밖이다.
        public void EarnGold(long amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "0 이상이어야 한다.");

            Gold = checked(Gold + amount);
        }

        // 결산(GameSession.Settle)이 판의 결산 뒤 성장도(Hq.NextStage)를 반영한다. 그대로이거나 1 오른다.
        internal void KeepGrowthStage(int stage)
        {
            if (stage < GrowthStage || stage > GrowthStage + 1)
                throw new ArgumentOutOfRangeException(nameof(stage), $"결산은 성장도를 그대로 두거나 1 올린다. 지금 {GrowthStage}, 받은 값 {stage}.");

            GrowthStage = stage;
        }
    }
}
