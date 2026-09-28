using System;
using System.Globalization;
using TMPro;
using UnityEngine.EventSystems;

namespace BlackHole.Unity
{
    // 결산 화면: 끝난 판의 결과(진행 시간, 블랙홀의 도달 Level·성장도 변화, 이정표로 끝났는가, 처치 수, 결산한 Gold)와
    // 결산 뒤의 진행 상태(Gold, 이정표 진행도, 산 노드 수)를 보여 주고, 계속하기를 알린다.
    // 보여 주기만 한다 — Gold는 이 화면이 열리기 전에 결산(GameSession.Settle)이 이미 더했다. ScreenFlow가 완료 사건에서 표시 값을 넘긴다.
    public sealed class SettlementScreen : UIRoot<SettlementScreen.Refs>
    {
        public enum Refs
        {
            ResultText,
            TimeText,
            KillsText,
            EarnedGoldText,
            TotalGoldText,
            ProgressText,
            ContinueBtn_Button,
        }

        public event Action ContinueClicked;

        private TMP_Text _result;
        private TMP_Text _time;
        private TMP_Text _earned;
        private TMP_Text _total;
        private TMP_Text _progress;

        protected override void OnInitialize()
        {
            ScreenRefs.WarnMissing<Refs>(this);

            _result = View.Text(Refs.ResultText);
            _time = View.Text(Refs.TimeText);
            _earned = View.Text(Refs.EarnedGoldText);
            _total = View.Text(Refs.TotalGoldText);
            _progress = View.Text(Refs.ProgressText);

            BindEvent(View.Button(Refs.ContinueBtn_Button), HandleContinueClicked);
        }

        private void HandleContinueClicked(PointerEventData _) => ContinueClicked?.Invoke();

        // milestone: 이정표에 닿아 끝난 판인가(남은 시간과 관계없이 끝났다).
        // stage → nextStage: 이 판의 성장도와 결산 뒤 성장도(목표 Level에 닿았으면 +1).
        public void ShowResult(float playedSeconds, int reachedLevel, int stage, int nextStage, bool milestone)
        {
            if (_result != null)
                _result.text = milestone ? "Milestone reached" : "Battle over";

            if (_time == null)
                return;

            string text = "Time  " + playedSeconds.ToString("F1", CultureInfo.InvariantCulture) + " s    Black hole  Lv "
                + reachedLevel.ToString(CultureInfo.InvariantCulture) + "    Stage " + stage.ToString(CultureInfo.InvariantCulture);

            if (nextStage != stage)
                text += " -> " + nextStage.ToString(CultureInfo.InvariantCulture);

            _time.text = text;
        }

        // earned: 이 판이 번 Gold. total: 결산을 마친 뒤 진행 상태의 Gold.
        // settled: 결산이 더한 Gold. 이정표로 끝난 판은 번 Gold(earned) 대신 이정표의 고정 보상이다. total: 결산을 마친 뒤 진행 상태의 Gold.
        public void ShowGold(long earned, long settled, bool milestone, long total)
        {
            if (_earned != null)
                _earned.text = milestone
                    ? "Milestone reward  +" + settled.ToString("N0", CultureInfo.InvariantCulture) + " Gold  (destroyed " + earned.ToString("N0", CultureInfo.InvariantCulture) + ")"
                    : "Earned  +" + earned.ToString("N0", CultureInfo.InvariantCulture) + " Gold";

            if (_total != null)
                _total.text = "Total  " + total.ToString("N0", CultureInfo.InvariantCulture) + " Gold";
        }

        // 결산 뒤의 진행: 이정표 진행도(닿은 수 / 전체)와 산 노드 수(산 수 / 전체).
        public void ShowProgress(int milestonesReached, int milestoneCount, int ownedNodes, int totalNodes)
        {
            if (_progress == null)
                return;

            string text = ownedNodes.ToString(CultureInfo.InvariantCulture) + " / " + totalNodes.ToString(CultureInfo.InvariantCulture) + " nodes";

            if (milestoneCount > 0)
                text = "Milestone " + milestonesReached.ToString(CultureInfo.InvariantCulture)
                    + " / " + milestoneCount.ToString(CultureInfo.InvariantCulture) + "    " + text;

            _progress.text = text;
        }
    }
}
