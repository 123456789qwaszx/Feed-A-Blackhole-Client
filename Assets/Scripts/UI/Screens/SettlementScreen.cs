using System;
using System.Collections;
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

        #region 0부터 일정시간동안 증가시키는 방식
        // 증가 순서
        private enum MatterType { Asteroid, Planet, Star, Done }
        private MatterType _matterType;

        private int _asteroidTarget;
        private int _planetTarget;
        private int _starTarget;

        private float _matterTimer;
        private const float MatterDuration = 1.0f;
        private bool _isShowMatter;

        private int targetOrder = 0;
        #endregion

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
            // 타겟값 저장 (증가 연출을 위해)
            _asteroidTarget = asteroids;
            _planetTarget = planets;
            _starTarget = stars;

            _matterTimer = 0f;
            _matterType = MatterType.Asteroid;
            _isShowMatter = true;
            
            if (_asteroidText != null)
                _asteroidText.text = Count(0);

            if (_planetText != null)
                _planetText.text = Count(0);

            if (_starText != null)
                _starText.text = Count(0);
        }

        // earned: 이 판이 번 Gold. settled: 결산이 더한 Gold(이정표로 끝났으면 이정표 보상). total: 결산 뒤 진행 상태의 Gold.
        public void ShowGold(long earned, long settled, bool milestone, long total)
        {
            if (_totalLabel != null)
                _totalLabel.text = milestone ? "REWARD" : "TOTAL";

            if (_earned != null)
            {

                _earned.text = Money(milestone ? settled : earned);
            }

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

        // MonoBehavior
        private void Update()
        {
            if (_isShowMatter)
            {
                _matterTimer += Time.deltaTime;

                float t = Mathf.Clamp01(_matterTimer / MatterDuration);

                // 부드럽게 증가
                t = Mathf.SmoothStep(0f, 1f, t);

                // 순서대로 증가 연출
                switch(_matterType)
                {
                    case MatterType.Asteroid:
                        IncreaseResult(_asteroidText, _asteroidTarget, t); break;
                    case MatterType.Planet:
                        IncreaseResult(_planetText, _planetTarget, t); break;
                    case MatterType.Star:
                        IncreaseResult(_starText, _starTarget, t); break;
                }
            }
        }

        private void IncreaseResult(TMP_Text _text, int _target, float t)
        {
            int value = IncreaseLerp(_target, t);
            if (_text != null)
                _text.text = Count(value);

            if (t >= 1f)
            {
                _text.text = Count(_target);

                _matterTimer = 0f;

                switch(_matterType)
                {
                    case MatterType.Asteroid:
                        _matterType = MatterType.Planet; break;
                    case MatterType.Planet:
                        _matterType = MatterType.Star; break;
                    case MatterType.Star:
                        _isShowMatter = false;
                        _matterType = MatterType.Done; break;
                }
            }
        }

        private int IncreaseLerp(int _target, float t) => Mathf.RoundToInt(Mathf.Lerp(0, _target, t));
    }
}
