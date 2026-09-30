using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    public sealed class UpgradeScreen : UIRoot<UpgradeScreen.Refs>, IUIPageOwner
    {
        public enum Refs
        {
            Gold_Text,
            Hq_Text,
            StartBattleBtn_Button,
            NodeTooltip_Image,
            NodeTooltipTitle_Text,
            NodeTooltipBody_Text,
            // 노치·둥근 모서리를 피하는 영역. 화면을 열 때와 해상도가 바뀔 때 UIManager가 Safe Area에 맞춘다(SafeAreaUtility).
            SafeAreaRoot,

            // Presentation이 바꾸는 그림과 글자. 코드는 건드리지 않는다.
            // 트리의 노드 그림은 여기 없다 — 트리 보기(NodeTreeView)의 NodeTreeLook이 정한다.
            Background_Image,
            GoldChip_Image,
            GoldIcon_Image,
            HqChip_Image,
            HqIcon_Image,
            StartBattleBtn_Image,
            StartBattleBtn_Text,
        }

        // 툴팁과 노드 사이의 틈.
        private const float TooltipGap = 8;

        public event Action StartBattleClicked;

        public RectTransform PageRoot => (RectTransform)transform;

        private TMP_Text _gold;
        private TMP_Text _hq;
        private RectTransform _tooltip;
        private TMP_Text _tooltipTitle;
        private TMP_Text _tooltipBody;
        private readonly Vector3[] _corners = new Vector3[4];

        protected override void OnInitialize()
        {
            ScreenRefs.WarnMissing<Refs>(this);

            _gold = View.Text(Refs.Gold_Text);
            _hq = View.Text(Refs.Hq_Text);
            _tooltip = View.Rect(Refs.NodeTooltip_Image);
            _tooltipTitle = View.Text(Refs.NodeTooltipTitle_Text);
            _tooltipBody = View.Text(Refs.NodeTooltipBody_Text);
            HideNodeTooltip();

            BindEvent(View.Button(Refs.StartBattleBtn_Button), HandleStartBattleClicked);
        }

        private void HandleStartBattleClicked(PointerEventData _) => StartBattleClicked?.Invoke();

        // 화면이 닫힐 때 툴팁도 닫는다. 다시 열 때 남아 있지 않게 한다.
        private void OnDisable() => HideNodeTooltip();

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
                + (goalLevel > 0
                    ? "  (goal Lv " + goalLevel.ToString(CultureInfo.InvariantCulture) + ")"
                    : "  (last stage)");
        }

        // 노드 위 가운데에 툴팁을 붙인다. 화면 위로 넘치면 노드 아래에 붙인다.
        // 툴팁은 레이캐스트를 받지 않아 노드의 올림을 가로채지 않는다.
        public void ShowNodeTooltip(RectTransform node, string title, string body)
        {
            if (_tooltip == null || node == null)
                return;

            if (_tooltipTitle != null)
                _tooltipTitle.text = title;

            if (_tooltipBody != null)
                _tooltipBody.text = body;

            _tooltip.gameObject.SetActive(true);
            _tooltip.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_tooltip);

            node.GetWorldCorners(_corners);
            Vector3 top = (_corners[1] + _corners[2]) * 0.5f;
            Vector3 bottom = (_corners[0] + _corners[3]) * 0.5f;

            _tooltip.pivot = new Vector2(0.5f, 0);
            _tooltip.position = top;
            _tooltip.anchoredPosition += new Vector2(0, TooltipGap);

            _tooltip.GetWorldCorners(_corners);
            float tooltipTop = _corners[1].y;
            ((RectTransform)transform).GetWorldCorners(_corners);

            if (tooltipTop > _corners[1].y)
            {
                _tooltip.pivot = new Vector2(0.5f, 1);
                _tooltip.position = bottom;
                _tooltip.anchoredPosition += new Vector2(0, -TooltipGap);
            }
        }

        public void HideNodeTooltip()
        {
            if (_tooltip != null)
                _tooltip.gameObject.SetActive(false);
        }
    }
}
