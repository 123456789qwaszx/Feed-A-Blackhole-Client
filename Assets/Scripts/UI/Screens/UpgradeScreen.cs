using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BlackHole.Unity
{
    // 업그레이드 화면(플레이어가 보는 노드 트리, 임시 모양). Gold, 블랙홀 성장도·목표 Level, 전투 시작 버튼을 가진다.
    // 노드 트리의 규칙을 모른다 — ScreenFlow가 표시 값을 넘기고, 전투 시작은 사건으로 알린다.
    // 페이지의 호스트다(IUIPageOwner). 어떤 페이지가 올라오는지는 모른다 — 지금은 ScreenFlow가 트리 보기(NodeTreeView)를 연다.
    // 페이지 자리(PageRoot)는 이 화면 자신이다. 페이지는 이 화면의 바로 아래 자식이어야 한다.
    public sealed class UpgradeScreen : UIRoot<UpgradeScreen.Refs>, IUIPageOwner
    {
        public enum Refs
        {
            GoldText,
            HqText,
            StartBattleBtn_Button,
        }

        public event Action StartBattleClicked;

        public RectTransform PageRoot => (RectTransform)transform;

        private TMP_Text _gold;
        private TMP_Text _hq;

        protected override void OnInitialize()
        {
            ScreenRefs.WarnMissing<Refs>(this);

            _gold = View.Text(Refs.GoldText);
            _hq = View.Text(Refs.HqText);

            BindEvent(View.Button(Refs.StartBattleBtn_Button), HandleStartBattleClicked);
        }

        private void HandleStartBattleClicked(PointerEventData _) => StartBattleClicked?.Invoke();

        public void ShowGold(long gold)
        {
            if (_gold != null)
                _gold.text = "Gold " + gold.ToString("N0", CultureInfo.InvariantCulture);
        }

        // 블랙홀(판 밖 진행): 성장도와 다음 판에서 성장도를 올리는 목표 Level(0이면 목표 없음).
        // 이정표 진행도와 산 노드 수는 결산 화면이 보여 준다.
        public void ShowHq(int stage, int goalLevel)
        {
            if (_hq == null)
                return;

            _hq.text = "Black hole  Stage " + stage.ToString(CultureInfo.InvariantCulture)
                + (goalLevel > 0 ? "  (goal Lv " + goalLevel.ToString(CultureInfo.InvariantCulture) + ")" : "  (last stage)");
        }
    }
}
