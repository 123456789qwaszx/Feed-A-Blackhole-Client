using System;
using System.Globalization;
using BlackHole.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    public sealed class UpgradeScreen : UIRoot<UpgradeScreen.Refs>, IUIPageOwner, ILocalizedView, IStyleView, INumberFormatView
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
            // 배경 위의 별빛·궤도 문양(UIScreenAmbient). 테마에 따라 머티리얼만 바뀐다.
            Ambient,
            // 조작 안내. 코드는 쓰지 않고 UI 글자 표가 언어에 맞게 바꾼다(ViewTexts).
            Hint_Text,

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
        // 고정 패널(터치): 노드 위가 아니라 왼쪽 고정 영역. 골드 칩 아래부터 안내 글자 위까지, 왼쪽을 칩에 맞춘다. 위아래 틈.
        private const float DockGap = 16;
        // 패널 안 구매 줄(고정했을 때만 보인다): 비용 글자 + 버튼. 버튼은 손가락 기준 최소 높이.
        private const float BuyHeight = 48;
        private const float BuyFontSize = 26;
        // 비용 글자 칸: 두 줄(비용 + Gold 부족분)까지. 한 줄이면 아래(버튼 쪽)에 붙는다.
        private const float CostHeight = 56;
        private const float CostGap = 6;
        // 패널 제목 왼쪽의 노드 그림 크기와 글자와의 틈.
        private const float IconSize = 40;
        private const float IconGap = 10;
        private const float DividerWidth = 2;

        public event Action StartBattleClicked;
        // 고정한 툴팁의 구매 버튼을 눌렀다.
        public event Action BuyConfirmed;

        public RectTransform PageRoot => (RectTransform)transform;

        private TMP_Text _gold;
        private TMP_Text _hq;
        private RectTransform _tooltip;
        private TMP_Text _tooltipTitle;
        private TMP_Text _tooltipBody;
        private Image _tooltipImage;
        private ContentSizeFitter _tooltipFitter;
        private TMP_Text _hint;
        // 올림 툴팁의 프리팹 모양(테두리 그림·색). 고정 패널은 그림을 떼고 단색 면으로 그리고, 풀면 되돌린다.
        private Sprite _tooltipSprite;
        private Color _tooltipColor;
        // 고정 패널에서만 보이는 것들: 제목 옆 노드 그림, 오른쪽 구분선, 본문과 구매 줄 사이를 채우는 빈 칸(구매 줄을 바닥에 붙인다).
        private Image _icon;
        private Image _divider;
        private GameObject _spacer;
        // 패널 안 구매 줄. 프리팹에 없고 코드가 툴팁 아래에 만든다(트리의 노드와 같은 방식). 버튼은 누르는 즉시 산다.
        private RectTransform _buy;
        private Button _buyButton;
        private Image _buyBack;
        private TMP_Text _buyLabel;
        private TMP_Text _buyCost;
        private float _titleMarginLeft;
        // 고정 패널 바탕(팔레트가 없을 때의 기본값): 바탕보다 살짝 밝은 면. 구분선은 글자색을 엷게.
        private static readonly Color PanelColor = new(0.16f, 0.17f, 0.19f, 0.94f);
        private static readonly Color DividerColor = new(1, 1, 1, 0.25f);
        // 구매 버튼 바탕: 살 수 있으면 강조색(초록), 아니면 글자색을 엷게 깐 회색. 팔레트가 없을 때의 기본값.
        private static readonly Color BuyReadyColor = new(0.34f, 0.59f, 0.41f, 0.9f);
        private static readonly Color BuyBlockedColor = new(1, 1, 1, 0.12f);
        // 툴팁을 터치로 고정했는가. 고정 중에는 올림으로 바뀌지 않고 레이캐스트를 받아 뒤의 노드를 가린다.
        public bool IsTooltipPinned { get; private set; }
        private NodeState _pinnedState;
        private readonly Vector3[] _corners = new Vector3[4];

        // 언어가 바뀌면 다시 쓸 마지막 값. 아직 보이지 않았으면 null.
        private Localizer _localizer;
        private readonly StyledTexts _styled = new();
        // 테마 색: 화면 바탕색(배경 그림 뒤).
        private readonly ThemedImages _themed = new();
        // 노드 툴팁의 프리팹 폭. 글자 크기 설정만큼 넓힌다(원작처럼 상자가 글자를 따라 커진다).
        private float _tooltipWidth;
        private long? _shownGold;
        private (int Stage, int GoalLevel)? _shownHq;
        // 트리에서 보고 있는 구역의 열쇠(번역표 node.region.<열쇠>). 있으면 블랙홀 글자 자리에 구역 이름을 보인다.
        private string _shownRegion;

        protected override void OnInitialize()
        {
            ScreenRefs.WarnMissing<Refs>(this);

            _gold = View.Text(Refs.Gold_Text);
            _hq = View.Text(Refs.Hq_Text);
            _tooltip = View.Rect(Refs.NodeTooltip_Image);
            _tooltipImage = View.Image(Refs.NodeTooltip_Image);
            _tooltipTitle = View.Text(Refs.NodeTooltipTitle_Text);
            _tooltipBody = View.Text(Refs.NodeTooltipBody_Text);
            _hint = View.Text(Refs.Hint_Text);
            CreatePanelParts();
            _styled.Add(_hint, UITextRole.Caption);
            _themed.AddColor(GetComponent<Image>(), UIColorRole.Backdrop);
            _tooltipWidth = _tooltip != null ? _tooltip.sizeDelta.x : 0;
            HideNodeTooltip();

            BindEvent(View.Button(Refs.StartBattleBtn_Button), HandleStartBattleClicked);
        }

        private void HandleStartBattleClicked(PointerEventData _) => StartBattleClicked?.Invoke();

        // 화면이 닫힐 때 툴팁도 닫는다. 다시 열 때 남아 있지 않게 한다.
        private void OnDisable() => HideNodeTooltip();

        public void ShowGold(long gold)
        {
            _shownGold = gold;

            if (_gold != null)
                _gold.text = Localized("upgrade.gold", GoldText.Of(gold));
        }

        // 블랙홀(판 밖 진행): 성장도(도달한 이정표 수)와 다음 판의 목표 Level(다음 이정표의 Level, 0이면 마지막 이정표 뒤).
        // 산 노드 수는 결산 화면이 보여 준다.
        public void ShowHq(int stage, int goalLevel)
        {
            _shownHq = (stage, goalLevel);
            PaintHeader();
        }

        // 트리에서 보고 있는 구역(열쇠). null이면 블랙홀 글자로 돌아간다. 트리 보기가 열려 있는 동안 ScreenFlow가 넣는다.
        public void ShowRegion(string key)
        {
            _shownRegion = key;
            PaintHeader();
        }

        // 위쪽 가운데 글자: 보고 있는 구역이 있으면 그 이름, 없으면 블랙홀 성장도·목표 Level.
        private void PaintHeader()
        {
            if (_hq == null)
                return;

            if (!string.IsNullOrEmpty(_shownRegion))
            {
                _hq.text = Localized("node.region." + _shownRegion);
                return;
            }

            if (_shownHq is not (int stage, int goalLevel))
                return;

            string shownStage = stage.ToString(CultureInfo.InvariantCulture);
            _hq.text = goalLevel > 0
                ? Localized("upgrade.hq.goal", shownStage, goalLevel.ToString(CultureInfo.InvariantCulture))
                : Localized("upgrade.hq.last", shownStage);
        }

        // 마지막으로 보인 Gold·블랙홀 글자를 지금 언어로 다시 쓴다. 노드 툴팁은 닫는다(다시 올리면 새 언어로 뜬다).
        // Gold 표기 설정이 바뀌었다. 보이는 Gold를 다시 쓴다.
        public void RefreshNumbers()
        {
            if (_shownGold is long gold)
                ShowGold(gold);
        }

        public void Relocalize(Localizer localizer)
        {
            _localizer = localizer;
            HideNodeTooltip();

            if (_shownGold is long gold)
                ShowGold(gold);

            PaintHeader();
        }

        // 글자 크기·언어별 글꼴. TextBindings에 등록되지 않은 글자만 여기서 맞춘다(등록된 글자는 Theme가 맞춘다).
        private UIScreenAmbient Ambient => View.Component<UIScreenAmbient>(Refs.Ambient);

        public void ApplyStyle(in UIStyle style)
        {
            Ambient?.Apply(style.ThemeId);
            _styled.SetStyle(style);
            _themed.SetStyle(style);
            PaintBuyButton();

            if (_tooltip != null)
                _tooltip.sizeDelta = new Vector2(_tooltipWidth * style.Scale, _tooltip.sizeDelta.y);
        }

        private string Localized(string key, params object[] args) =>
            _localizer != null ? _localizer.Format(key, args) : key;

        // 올림 툴팁: 노드 위 가운데에 붙인다. 화면 위로 넘치면 노드 아래에 붙인다.
        // 레이캐스트를 받지 않아 노드의 올림을 가로채지 않는다. 구매 버튼은 없다(PC는 노드를 눌러 산다). 고정 중이면 무시한다.
        public void ShowNodeTooltip(RectTransform node, string title, string body)
        {
            if (IsTooltipPinned)
                return;

            Place(node, title, body, pinned: false);
        }

        // 고정 툴팁(터치): 왼쪽 고정 영역(골드 칩 아래)에 두고 다른 곳을 누를 때까지 남는다. 레이캐스트를 받아 뒤의 노드를 가리며, 구매 버튼이 보인다.
        // 손가락이 노드 주변을 가리는 터치에서는 노드 위에 띄우는 것보다 늘 같은 자리에 있는 편이 읽고 누르기 쉽다.
        // state: 버튼 글자·활성을 정한다(Purchasable 구매 / Revealed 구매(흐림) / Owned 최대 / Hidden 잠김).
        // icon: 트리에 그려진 노드 그림(제목 옆). cost: 비용 줄("비용 70" / "최대" / 부족분). state: 버튼 글자·활성.
        public void PinNodeTooltip(RectTransform node, Sprite icon, string title, string body, string cost, NodeState state)
        {
            _pinnedState = state;

            if (_icon != null)
            {
                _icon.sprite = icon;
                _icon.enabled = icon != null;
            }

            if (_buyCost != null)
                _buyCost.text = cost;

            Place(node, title, body, pinned: true);
        }

        private void Place(RectTransform node, string title, string body, bool pinned)
        {
            if (_tooltip == null || node == null)
                return;

            IsTooltipPinned = pinned;

            if (_tooltipTitle != null)
                _tooltipTitle.text = title;

            if (_tooltipBody != null)
            {
                _tooltipBody.text = body;
                // 본문이 없으면(아직 닿지 않은 노드는 이름만) 글자 칸을 접어 상자가 제목만큼만 되게 한다.
                _tooltipBody.gameObject.SetActive(!string.IsNullOrEmpty(body));
            }

            SetPanelLook(pinned);
            PaintBuyButton();

            _tooltip.gameObject.SetActive(true);
            _tooltip.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_tooltip);

            if (pinned)
            {
                DockLeft();
                return;
            }

            // 올림 툴팁의 자리: 노드 위 → (위로 넘치면) 노드 오른쪽 → (오른쪽으로 넘치면) 노드 아래. 화면 안에 드는 첫 자리를 쓴다.
            node.GetWorldCorners(_corners);
            Vector3 top = (_corners[1] + _corners[2]) * 0.5f;
            Vector3 bottom = (_corners[0] + _corners[3]) * 0.5f;
            Vector3 right = (_corners[2] + _corners[3]) * 0.5f;

            var screen = (RectTransform)transform;

            Attach(new Vector2(0.5f, 0), top, new Vector2(0, TooltipGap));
            if (!Overflows(screen, Edge.Top))
                return;

            Attach(new Vector2(0, 0.5f), right, new Vector2(TooltipGap, 0));
            if (!Overflows(screen, Edge.Right))
                return;

            Attach(new Vector2(0.5f, 1), bottom, new Vector2(0, -TooltipGap));
        }

        // 왼쪽 고정 자리: 위는 골드 칩 아래(틈만큼), 아래는 안내 글자 위(틈만큼), 왼쪽은 칩에 맞춘다. 높이는 그 사이를 다 쓴다
        // — 설명 길이와 무관하게 구매 줄이 늘 바닥에 온다. 칩·안내가 없으면 화면 가장자리에서 틈만큼 안쪽.
        private void DockLeft()
        {
            var parent = (RectTransform)_tooltip.parent;
            var screen = (RectTransform)transform;
            RectTransform chip = View.Rect(Refs.GoldChip_Image);

            screen.GetWorldCorners(_corners);
            Vector3 topLeft = parent.InverseTransformPoint(_corners[1]) + new Vector3(DockGap, -DockGap, 0);
            float bottom = parent.InverseTransformPoint(_corners[0]).y + DockGap;

            if (chip != null)
            {
                chip.GetWorldCorners(_corners);
                topLeft = parent.InverseTransformPoint(_corners[0]) + new Vector3(0, -DockGap, 0);
            }

            if (_hint != null)
            {
                ((RectTransform)_hint.transform).GetWorldCorners(_corners);
                bottom = parent.InverseTransformPoint(_corners[1]).y + DockGap;
            }

            float height = Mathf.Max(topLeft.y - bottom, LayoutUtility.GetPreferredHeight(_tooltip));

            _tooltip.pivot = new Vector2(0, 1);
            _tooltip.sizeDelta = new Vector2(_tooltip.sizeDelta.x, height);
            _tooltip.localPosition = new Vector3(topLeft.x, topLeft.y, 0);
            // 높이가 바뀌었으니 빈 칸이 늘어나 구매 줄이 바닥에 오도록 지금 다시 배치한다.
            LayoutRebuilder.ForceRebuildLayoutImmediate(_tooltip);
        }

        private enum Edge { Top, Right }

        // 툴팁의 pivot을 두고 노드의 한 점에 붙인 뒤 틈만큼 띄운다.
        private void Attach(Vector2 pivot, Vector3 anchor, Vector2 gap)
        {
            _tooltip.pivot = pivot;
            _tooltip.position = anchor;
            _tooltip.anchoredPosition += gap;
        }

        // 툴팁이 화면(이 루트)의 그 가장자리 밖으로 나가는가.
        private bool Overflows(RectTransform screen, Edge edge)
        {
            _tooltip.GetWorldCorners(_corners);
            float tooltipTop = _corners[1].y;
            float tooltipRight = _corners[2].x;
            screen.GetWorldCorners(_corners);

            return edge == Edge.Top ? tooltipTop > _corners[1].y : tooltipRight > _corners[2].x;
        }

        public void HideNodeTooltip()
        {
            IsTooltipPinned = false;
            SetPanelLook(false);

            if (_tooltip != null)
                _tooltip.gameObject.SetActive(false);
        }

        // 올림 툴팁 ↔ 고정 패널의 모양을 바꾼다. 패널: 테두리 그림 대신 단색 면, 레이캐스트 받음, 그림·구분선·빈 칸 켜고, 안내 글자 숨김,
        // 높이는 DockLeft가 정하므로 크기 맞춤을 끈다. 툴팁: 전부 되돌린다.
        private void SetPanelLook(bool panel)
        {
            if (_tooltipImage != null)
            {
                _tooltipImage.raycastTarget = panel;
                _tooltipImage.sprite = panel ? null : _tooltipSprite;
                _tooltipImage.color = panel ? PanelBackColor() : _tooltipColor;
            }

            if (_tooltipFitter != null)
                _tooltipFitter.verticalFit = panel ? ContentSizeFitter.FitMode.Unconstrained : ContentSizeFitter.FitMode.PreferredSize;

            if (_tooltipTitle != null)
            {
                Vector4 margin = _tooltipTitle.margin;
                margin.x = panel ? _titleMarginLeft + IconSize + IconGap : _titleMarginLeft;
                _tooltipTitle.margin = margin;
            }

            if (_icon != null && _icon.gameObject.activeSelf != panel)
                _icon.gameObject.SetActive(panel);

            if (_divider != null && _divider.gameObject.activeSelf != panel)
                _divider.gameObject.SetActive(panel);

            if (_spacer != null && _spacer.activeSelf != panel)
                _spacer.SetActive(panel);

            if (_hint != null && _hint.gameObject.activeSelf == panel)
                _hint.gameObject.SetActive(!panel);
        }

        private Color PanelBackColor()
        {
            Color? themed = _themed.Style.ColorFor(UIColorRole.Surface);

            if (themed is not Color color)
                return PanelColor;

            color.a = PanelColor.a;
            return color;
        }

        private void HandleBuyClicked(PointerEventData _) => BuyConfirmed?.Invoke();

        // 고정 패널에서만 쓰는 부품을 프리팹 툴팁 아래에 만든다(트리의 노드와 같은 방식). 그림 없이 단색으로 만들고
        // 테마 색(ThemedImages)과 글꼴(StyledTexts)을 받는다. 올림 툴팁에서는 모두 숨긴다.
        // - 제목 옆 노드 그림(제목 글자의 왼쪽 여백에 놓는다)
        // - 오른쪽 세로 구분선
        // - 본문 뒤의 빈 칸(flexibleHeight): 구매 줄을 패널 바닥으로 민다
        // - 구매 줄: 비용 글자 + 버튼. 버튼은 누르는 즉시 산다(꾹 누름은 노드 쪽이다).
        private void CreatePanelParts()
        {
            if (_tooltip == null)
                return;

            _tooltipFitter = _tooltip.GetComponent<ContentSizeFitter>();

            if (_tooltipImage != null)
            {
                _tooltipSprite = _tooltipImage.sprite;
                _tooltipColor = _tooltipImage.color;
            }

            if (_tooltipTitle != null)
            {
                _titleMarginLeft = _tooltipTitle.margin.x;

                RectTransform iconRect = Child((RectTransform)_tooltipTitle.transform, "Icon");
                iconRect.anchorMin = new Vector2(0, 0.5f);
                iconRect.anchorMax = new Vector2(0, 0.5f);
                iconRect.pivot = new Vector2(0, 0.5f);
                iconRect.anchoredPosition = new Vector2(_titleMarginLeft, 0);
                iconRect.sizeDelta = new Vector2(IconSize, IconSize);
                _icon = iconRect.gameObject.AddComponent<Image>();
                _icon.preserveAspect = true;
                _icon.raycastTarget = false;
                iconRect.gameObject.SetActive(false);
            }

            RectTransform dividerRect = Child(_tooltip, "Divider");
            dividerRect.anchorMin = new Vector2(1, 0);
            dividerRect.anchorMax = new Vector2(1, 1);
            dividerRect.pivot = new Vector2(1, 0.5f);
            dividerRect.anchoredPosition = Vector2.zero;
            dividerRect.sizeDelta = new Vector2(DividerWidth, 0);
            var dividerLayout = dividerRect.gameObject.AddComponent<LayoutElement>();
            dividerLayout.ignoreLayout = true;
            _divider = dividerRect.gameObject.AddComponent<Image>();
            _divider.color = DividerColor;
            _divider.raycastTarget = false;
            _themed.AddColor(_divider, UIColorRole.TextMain);
            dividerRect.gameObject.SetActive(false);

            RectTransform spacer = Child(_tooltip, "Spacer");
            var spacerLayout = spacer.gameObject.AddComponent<LayoutElement>();
            spacerLayout.flexibleHeight = 1;
            spacerLayout.minHeight = 0;
            _spacer = spacer.gameObject;
            _spacer.SetActive(false);

            var buy = new GameObject("NodeTooltipBuy_Button", typeof(RectTransform));
            _buy = (RectTransform)buy.transform;
            _buy.SetParent(_tooltip, false);

            var layout = buy.AddComponent<LayoutElement>();
            layout.minHeight = CostHeight + CostGap + BuyHeight;
            layout.preferredHeight = CostHeight + CostGap + BuyHeight;

            // 비용 글자: 줄의 위쪽. 버튼: 줄의 아래쪽.
            RectTransform costRect = Child(_buy, "Cost");
            costRect.anchorMin = new Vector2(0, 1);
            costRect.anchorMax = new Vector2(1, 1);
            costRect.pivot = new Vector2(0.5f, 1);
            costRect.sizeDelta = new Vector2(0, CostHeight);
            _buyCost = costRect.gameObject.AddComponent<TextMeshProUGUI>();
            _buyCost.alignment = TextAlignmentOptions.BottomLeft;
            _buyCost.textWrappingMode = TextWrappingModes.Normal;
            _buyCost.overflowMode = TextOverflowModes.Truncate;
            _buyCost.raycastTarget = false;
            if (_tooltipBody != null)
            {
                _buyCost.font = _tooltipBody.font;
                _buyCost.fontSize = _tooltipBody.fontSize;
                _buyCost.color = _tooltipBody.color;
            }
            _styled.Add(_buyCost, UITextRole.Body);

            RectTransform back = Child(_buy, "Back");
            back.anchorMin = new Vector2(0, 0);
            back.anchorMax = new Vector2(1, 0);
            back.pivot = new Vector2(0.5f, 0);
            back.sizeDelta = new Vector2(0, BuyHeight);

            var backImage = back.gameObject.AddComponent<Image>();
            backImage.color = BuyBlockedColor;
            backImage.raycastTarget = true;
            // 색은 상태에 따라 바뀌므로 ThemedImages에 넣지 않고 PaintBuyButton이 팔레트에서 직접 고른다.
            _buyBack = backImage;

            RectTransform label = Child(back, "Label");
            label.anchorMin = Vector2.zero;
            label.anchorMax = Vector2.one;
            label.sizeDelta = Vector2.zero;

            _buyLabel = label.gameObject.AddComponent<TextMeshProUGUI>();
            _buyLabel.fontSize = BuyFontSize;
            _buyLabel.alignment = TextAlignmentOptions.Center;
            _buyLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _buyLabel.raycastTarget = false;
            if (_tooltipBody != null)
            {
                _buyLabel.font = _tooltipBody.font;
                _buyLabel.color = _tooltipBody.color;
            }
            _styled.Add(_buyLabel, UITextRole.Body);

            _buyButton = back.gameObject.AddComponent<Button>();
            _buyButton.targetGraphic = backImage;
            _buyButton.transition = Selectable.Transition.None;
            BindEvent(_buyButton, HandleBuyClicked);

            _buy.gameObject.SetActive(false);
        }

        // 고정 상태에 맞춰 구매 줄을 보이고 글자·활성을 정한다. 올림 툴팁이면 숨긴다.
        private void PaintBuyButton()
        {
            if (_buy == null)
                return;

            if (!IsTooltipPinned)
            {
                _buy.gameObject.SetActive(false);
                return;
            }

            bool interactable = _pinnedState == NodeState.Purchasable;
            string key = _pinnedState switch
            {
                NodeState.Owned => "node.maxed",
                NodeState.Hidden => "node.locked",
                _ => "node.buy",
            };

            _buyButton.interactable = interactable;
            _buyBack.color = BuyBackColor(interactable);
            _buyLabel.text = Localized(key);
            _buyLabel.alpha = interactable ? 1 : 0.45f;
            _buy.gameObject.SetActive(true);
        }

        // 살 수 있으면 팔레트의 강조색(알파는 기본값), 아니면 글자색을 엷게(회색).
        private Color BuyBackColor(bool ready)
        {
            UIStyle style = _themed.Style;
            Color fallback = ready ? BuyReadyColor : BuyBlockedColor;
            Color? themed = style.ColorFor(ready ? UIColorRole.Accent : UIColorRole.TextMain);

            if (themed is not Color color)
                return fallback;

            color.a = fallback.a;
            return color;
        }

        private static RectTransform Child(RectTransform parent, string name)
        {
            var child = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)child.transform;
            rect.SetParent(parent, false);
            return rect;
        }
    }
}
