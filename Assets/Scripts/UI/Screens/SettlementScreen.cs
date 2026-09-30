using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    public sealed class SettlementScreen : UIRoot<SettlementScreen.Refs>
    {
        public enum Refs
        {
            ResultText,
            StageFill,
            StageText,
            AsteroidRow_Button,
            AsteroidRow_Text,
            PlanetRow_Button,
            PlanetRow_Text,
            StarRow_Button,
            StarRow_Text,
            TotalLabelText,
            EarnedGoldText,
            TotalGoldText,
            UpgradeBtn_Button,
            UpgradeBtn_Text,
            ContinueBtn_Button,
            Tooltip,
            TooltipText,
            // 노치·둥근 모서리를 피하는 영역. 화면을 열 때와 해상도가 바뀔 때 UIManager가 Safe Area에 맞춘다(SafeAreaUtility).
            SafeAreaRoot,
        }

        // 툴팁과 행 사이의 틈.
        private const float TooltipGap = 6;

        private readonly Vector3[] _corners = new Vector3[4];

        private TMP_Text _result;
        private RectTransform _stageFill;
        private TMP_Text _stageText;
        private TMP_Text _totalLabel;
        private TMP_Text _earned;
        private TMP_Text _totalGold;
        private TMP_Text _upgradeLabel;
        private TMP_Text _asteroidText;
        private TMP_Text _planetText;
        private TMP_Text _starText;

        private Button _asteroidRow;
        private Button _planetRow;
        private Button _starRow;
        private Button _upgradeButton;
        private Button _continueButton;
        private ButtonAnimation _asteroidAnimation;
        private ButtonAnimation _planetAnimation;
        private ButtonAnimation _starAnimation;
        private ButtonAnimation _upgradeAnimation;
        private ButtonAnimation _continueAnimation;

        private RectTransform _tooltip;
        private TMP_Text _tooltipText;
        private RectTransform _tooltipOwner;

        public event Action UpgradeClicked;
        public event Action ContinueClicked;

        protected override void OnInitialize()
        {
            ScreenRefs.WarnMissing<Refs>(this);

            _result = View.Text(Refs.ResultText);
            _stageFill = View.Rect(Refs.StageFill);
            _stageText = View.Text(Refs.StageText);
            _totalLabel = View.Text(Refs.TotalLabelText);
            _earned = View.Text(Refs.EarnedGoldText);
            _totalGold = View.Text(Refs.TotalGoldText);
            _upgradeLabel = View.Text(Refs.UpgradeBtn_Text);
            _asteroidText = View.Text(Refs.AsteroidRow_Text);
            _planetText = View.Text(Refs.PlanetRow_Text);
            _starText = View.Text(Refs.StarRow_Text);
            _tooltip = View.Rect(Refs.Tooltip);
            _tooltipText = View.Text(Refs.TooltipText);
            HideTooltip();

            _asteroidRow = View.Button(Refs.AsteroidRow_Button);
            _planetRow = View.Button(Refs.PlanetRow_Button);
            _starRow = View.Button(Refs.StarRow_Button);
            _upgradeButton = View.Button(Refs.UpgradeBtn_Button);
            _continueButton = View.Button(Refs.ContinueBtn_Button);

            _asteroidAnimation = ButtonAnimation.Of(_asteroidRow);
            _planetAnimation = ButtonAnimation.Of(_planetRow);
            _starAnimation = ButtonAnimation.Of(_starRow);
            _upgradeAnimation = ButtonAnimation.Of(_upgradeButton);
            _continueAnimation = ButtonAnimation.Of(_continueButton);

            BindEvent(_asteroidRow, HoverAsteroidRow, ETouchEvent.PointerEnter);
            BindEvent(_asteroidRow, LeaveAsteroidRow, ETouchEvent.PointerExit);
            BindEvent(_asteroidRow, PressAsteroidRow, ETouchEvent.PointerDown);
            BindEvent(_asteroidRow, ReleaseAsteroidRow, ETouchEvent.PointerUp);

            BindEvent(_planetRow, HoverPlanetRow, ETouchEvent.PointerEnter);
            BindEvent(_planetRow, LeavePlanetRow, ETouchEvent.PointerExit);
            BindEvent(_planetRow, PressPlanetRow, ETouchEvent.PointerDown);
            BindEvent(_planetRow, ReleasePlanetRow, ETouchEvent.PointerUp);

            BindEvent(_starRow, HoverStarRow, ETouchEvent.PointerEnter);
            BindEvent(_starRow, LeaveStarRow, ETouchEvent.PointerExit);
            BindEvent(_starRow, PressStarRow, ETouchEvent.PointerDown);
            BindEvent(_starRow, ReleaseStarRow, ETouchEvent.PointerUp);

            BindEvent(_upgradeButton, ClickUpgradeButton);
            BindEvent(_upgradeButton, HoverUpgradeButton, ETouchEvent.PointerEnter);
            BindEvent(_upgradeButton, LeaveUpgradeButton, ETouchEvent.PointerExit);
            BindEvent(_upgradeButton, PressUpgradeButton, ETouchEvent.PointerDown);
            BindEvent(_upgradeButton, ReleaseUpgradeButton, ETouchEvent.PointerUp);

            BindEvent(_continueButton, ClickContinueButton);
            BindEvent(_continueButton, HoverContinueButton, ETouchEvent.PointerEnter);
            BindEvent(_continueButton, LeaveContinueButton, ETouchEvent.PointerExit);
            BindEvent(_continueButton, PressContinueButton, ETouchEvent.PointerDown);
            BindEvent(_continueButton, ReleaseContinueButton, ETouchEvent.PointerUp);
        }

        // 화면이 닫힐 때 툴팁도 닫는다. 다시 열 때 남아 있지 않게 한다.
        private void OnDisable() => HideTooltip();

        private void HoverAsteroidRow(PointerEventData _)
        {
            _asteroidAnimation.Hover();
            ShowTooltip(_asteroidRow, "Asteroid");
        }

        private void LeaveAsteroidRow(PointerEventData _)
        {
            _asteroidAnimation.Leave();
            HideTooltip(_asteroidRow);
        }

        private void PressAsteroidRow(PointerEventData _) => _asteroidAnimation.Press();
        private void ReleaseAsteroidRow(PointerEventData _) => _asteroidAnimation.Release();

        private void HoverPlanetRow(PointerEventData _)
        {
            _planetAnimation.Hover();
            ShowTooltip(_planetRow, "Planet");
        }

        private void LeavePlanetRow(PointerEventData _)
        {
            _planetAnimation.Leave();
            HideTooltip(_planetRow);
        }

        private void PressPlanetRow(PointerEventData _) => _planetAnimation.Press();
        private void ReleasePlanetRow(PointerEventData _) => _planetAnimation.Release();

        private void HoverStarRow(PointerEventData _)
        {
            _starAnimation.Hover();
            ShowTooltip(_starRow, "Star");
        }

        private void LeaveStarRow(PointerEventData _)
        {
            _starAnimation.Leave();
            HideTooltip(_starRow);
        }

        private void PressStarRow(PointerEventData _) => _starAnimation.Press();
        private void ReleaseStarRow(PointerEventData _) => _starAnimation.Release();

        private void ClickUpgradeButton(PointerEventData _) => UpgradeClicked?.Invoke();
        private void HoverUpgradeButton(PointerEventData _) => _upgradeAnimation.Hover();
        private void LeaveUpgradeButton(PointerEventData _) => _upgradeAnimation.Leave();
        private void PressUpgradeButton(PointerEventData _) => _upgradeAnimation.Press();
        private void ReleaseUpgradeButton(PointerEventData _) => _upgradeAnimation.Release();

        private void ClickContinueButton(PointerEventData _) => ContinueClicked?.Invoke();
        private void HoverContinueButton(PointerEventData _) => _continueAnimation.Hover();
        private void LeaveContinueButton(PointerEventData _) => _continueAnimation.Leave();
        private void PressContinueButton(PointerEventData _) => _continueAnimation.Press();
        private void ReleaseContinueButton(PointerEventData _) => _continueAnimation.Release();

        // milestone: 이정표에 닿아 끝난 판인가(남은 시간과 관계없이 끝났다).
        public void ShowResult(bool milestone)
        {
            if (_result != null)
            {
                _result.text = milestone
                    ? "Milestone reached"
                    : "Battle over";
            }
        }

        // 블랙홀 성장도 막대: 결산 뒤 성장도 / 마지막 성장도. 이번 판에 올랐으면 "1 -> 2"로 보인다.
        public void ShowStage(int stage, int nextStage, int maxStage)
        {
            if (_stageFill != null)
            {
                float fill = maxStage > 0
                    ? Mathf.Clamp01(nextStage / (float)maxStage)
                    : 1;

                _stageFill.anchorMax = new Vector2(fill, _stageFill.anchorMax.y);
            }

            if (_stageText == null)
                return;

            string reached = nextStage != stage
                ? stage.ToString(CultureInfo.InvariantCulture) + " -> " + nextStage.ToString(CultureInfo.InvariantCulture)
                : nextStage.ToString(CultureInfo.InvariantCulture);

            _stageText.text = "Stage " + reached + " / " + maxStage.ToString(CultureInfo.InvariantCulture);
        }

        // 물질 행: 물질별 처치 수. 판 기록에 종류별 Gold가 생기면 Gold로 바꾼다.
        public void ShowMatter(int asteroids, int planets, int stars)
        {
            if (_asteroidText != null)
                _asteroidText.text = Count(asteroids);

            if (_planetText != null)
                _planetText.text = Count(planets);

            if (_starText != null)
                _starText.text = Count(stars);
        }

        // earned: 이 판이 번 Gold. settled: 결산이 더한 Gold(이정표로 끝났으면 이정표 보상). total: 결산 뒤 진행 상태의 Gold.
        public void ShowGold(long earned, long settled, bool milestone, long total)
        {
            if (_totalLabel != null)
                _totalLabel.text = milestone ? "REWARD" : "TOTAL";

            if (_earned != null)
                _earned.text = Money(milestone ? settled : earned);

            if (_totalGold != null)
                _totalGold.text = Money(total);
        }

        // 지금 Gold로 살 수 있는 노드 수. 없으면 수를 붙이지 않는다.
        public void ShowUpgradeCount(int purchasable)
        {
            if (_upgradeLabel != null)
            {
                _upgradeLabel.text = purchasable > 0
                    ? "Upgrade (" + purchasable.ToString(CultureInfo.InvariantCulture) + ")"
                    : "Upgrade";
            }
        }

        #region 툴팁

        // 행 위 가운데에 붙인다. 툴팁은 레이캐스트를 받지 않아 행의 올림을 가로채지 않는다.
        private void ShowTooltip(Button row, string displayName)
        {
            if (_tooltip == null || row == null)
                return;

            _tooltipOwner = (RectTransform)row.transform;

            if (_tooltipText != null)
                _tooltipText.text = displayName;

            _tooltip.gameObject.SetActive(true);
            _tooltip.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_tooltip);

            _tooltipOwner.GetWorldCorners(_corners);
            _tooltip.pivot = new Vector2(0.5f, 0);
            _tooltip.position = (_corners[1] + _corners[2]) * 0.5f;
            _tooltip.anchoredPosition += new Vector2(0, TooltipGap);
        }

        // 다른 행으로 막 옮겨 간 툴팁은 닫지 않는다.
        private void HideTooltip(Button row)
        {
            if (row != null && row.transform == _tooltipOwner)
                HideTooltip();
        }

        private void HideTooltip()
        {
            _tooltipOwner = null;

            if (_tooltip != null)
                _tooltip.gameObject.SetActive(false);
        }

        #endregion

        private static string Count(int count) =>
            "x" + count.ToString("N0", CultureInfo.InvariantCulture);

        private static string Money(long gold) =>
            "$" + gold.ToString("N0", CultureInfo.InvariantCulture);
    }
}
