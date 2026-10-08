using System;
using System.Globalization;
using BlackHole.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    public sealed class SettlementScreen : UIRoot<SettlementScreen.Refs>
    {
        public enum Refs
        {
            Result_Text,
            // 성장도 막대의 채움. 폭은 anchorMax.x로 정한다.
            StageFill_Image,
            Stage_Text,
            AsteroidRow_Button,
            AsteroidRow_Text,
            PlanetRow_Button,
            PlanetRow_Text,
            StarRow_Button,
            StarRow_Text,
            TotalLabel_Text,
            EarnedGold_Text,
            TotalGold_Text,
            UpgradeBtn_Button,
            UpgradeBtn_Text,
            ContinueBtn_Button,
            Tooltip_Image,
            Tooltip_Text,
            // 노치·둥근 모서리를 피하는 영역. 화면을 열 때와 해상도가 바뀔 때 UIManager가 Safe Area에 맞춘다(SafeAreaUtility).
            SafeAreaRoot,
            // 오른쪽 통계 칸(스킬별 피해·수집). 칸에 손을 올리면 툴팁으로 이름을 보인다.
            ElectricStarDamage_Button,
            ElectricStarDamage_Text,
            LaserDamage_Button,
            LaserDamage_Text,
            SupernovaDamage_Button,
            SupernovaDamage_Text,
            GoldenAsteroidGold_Button,
            GoldenAsteroidGold_Text,
            ElectricAsteroidDamage_Button,
            ElectricAsteroidDamage_Text,
            BreakerDamage_Button,
            BreakerDamage_Text,
            BreakerCritDamage_Button,
            BreakerCritDamage_Text,
            BreakerClicks_Button,
            BreakerClicks_Text,
            DestroyedStars_Button,
            DestroyedStars_Text,
            DestroyedPlanets_Button,
            DestroyedPlanets_Text,
            CollectedMoons_Button,
            CollectedMoons_Text,
            CollectedComets_Button,
            CollectedComets_Text,
            DestroyedAsteroids_Button,
            DestroyedAsteroids_Text,
            AddedTime_Button,
            AddedTime_Text,

            // Presentation이 바꾸는 그림과 글자. 코드는 건드리지 않는다.
            Background_Image,
            AsteroidRow_Image,
            AsteroidIcon_Image,
            PlanetRow_Image,
            PlanetIcon_Image,
            StarRow_Image,
            StarIcon_Image,
            GoldIcon_Image,
            StageBar_Image,
            UpgradeBtn_Image,
            ContinueBtn_Image,
            ContinueBtn_Text,
            ElectricStarDamage_Image,
            ElectricStarDamageIcon_Image,
            LaserDamage_Image,
            LaserDamageIcon_Image,
            SupernovaDamage_Image,
            SupernovaDamageIcon_Image,
            GoldenAsteroidGold_Image,
            GoldenAsteroidGoldIcon_Image,
            ElectricAsteroidDamage_Image,
            ElectricAsteroidDamageIcon_Image,
            BreakerDamage_Image,
            BreakerDamageIcon_Image,
            BreakerCritDamage_Image,
            BreakerCritDamageIcon_Image,
            BreakerClicks_Image,
            BreakerClicksIcon_Image,
            DestroyedStars_Image,
            DestroyedStarsIcon_Image,
            DestroyedPlanets_Image,
            DestroyedPlanetsIcon_Image,
            CollectedMoons_Image,
            CollectedMoonsIcon_Image,
            CollectedComets_Image,
            CollectedCometsIcon_Image,
            DestroyedAsteroids_Image,
            DestroyedAsteroidsIcon_Image,
            AddedTime_Image,
            AddedTimeIcon_Image,
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

        private StatCell _electricStarDamage;
        private StatCell _laserDamage;
        private StatCell _supernovaDamage;
        private StatCell _goldenAsteroidGold;
        private StatCell _electricAsteroidDamage;
        private StatCell _breakerDamage;
        private StatCell _breakerCritDamage;
        private StatCell _breakerClicks;
        private StatCell _destroyedStars;
        private StatCell _destroyedPlanets;
        private StatCell _collectedMoons;
        private StatCell _collectedComets;
        private StatCell _destroyedAsteroids;
        private StatCell _addedTime;

        // 오른쪽 통계 칸 하나: 칸 버튼(툴팁·연출), 값 글자, 툴팁 이름.
        private sealed class StatCell
        {
            public Button Button { get; }
            public ButtonAnimation Animation { get; }
            public TMP_Text Value { get; }
            public string Label { get; }

            public StatCell(Button button, TMP_Text value, string label)
            {
                Button = button;
                Animation = ButtonAnimation.Of(button);
                Value = value;
                Label = label;
            }
        }

        public event Action UpgradeClicked;
        public event Action ContinueClicked;

        #region 0부터 일정시간동안 증가시키는 방식
        // 증가 순서
        private enum MatterType { Asteroid, Planet, Star, Earned, Total, Done }
        private MatterType _matterType;

        // Stage바 증가
        private float _stageFillTarget;
        private float _stageFillTimer;
        private const float DefaultDuration = 1.0f;
        private float _stageFillDuration = DefaultDuration;
        private bool _isShowingStage;

        // 행성 파괴
        private int _asteroidTarget;
        private int _planetTarget;
        private int _starTarget;

        // 획득 보상
        private long _earnedTarget;
        private long _totalBegin;
        private long _totalTarget;

        // 타이머
        private float _matterTimer;
        private float _matterDuration = DefaultDuration;
        private bool _isShowMatter;

        // 확대 효과 (강조 표시를 위해 짠 하고 나타나는 효과
        private readonly Dictionary<TMP_Text, int> _previousKills = new();
        private readonly Dictionary<TMP_Text, long> _previousGolds = new();
        private TMP_Text _scaleTarget;
        private float _scaleTimer;

        private const float MatterStepCount = 5; // Matter 개수
        private const float ScaleDuration = 0.15f;
        private const float HighlightScale = 1.2f;
        private bool _isScaleEffect;
        #endregion

        protected override void OnInitialize()
        {
            ScreenRefs.WarnMissing<Refs>(this);

            _result = View.Text(Refs.Result_Text);
            _stageFill = View.Rect(Refs.StageFill_Image);
            _stageText = View.Text(Refs.Stage_Text);
            _totalLabel = View.Text(Refs.TotalLabel_Text);
            _earned = View.Text(Refs.EarnedGold_Text);
            _totalGold = View.Text(Refs.TotalGold_Text);
            _upgradeLabel = View.Text(Refs.UpgradeBtn_Text);
            _asteroidText = View.Text(Refs.AsteroidRow_Text);
            _planetText = View.Text(Refs.PlanetRow_Text);
            _starText = View.Text(Refs.StarRow_Text);
            _tooltip = View.Rect(Refs.Tooltip_Image);
            _tooltipText = View.Text(Refs.Tooltip_Text);
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

            _electricStarDamage = BindStat(Refs.ElectricStarDamage_Button, Refs.ElectricStarDamage_Text, "전기 별 대미지");
            _laserDamage = BindStat(Refs.LaserDamage_Button, Refs.LaserDamage_Text, "레이저 대미지");
            _supernovaDamage = BindStat(Refs.SupernovaDamage_Button, Refs.SupernovaDamage_Text, "슈퍼노바 대미지");
            _goldenAsteroidGold = BindStat(Refs.GoldenAsteroidGold_Button, Refs.GoldenAsteroidGold_Text, "황금 소행성에서 얻는 돈");
            _electricAsteroidDamage = BindStat(Refs.ElectricAsteroidDamage_Button, Refs.ElectricAsteroidDamage_Text, "전기 소행성 대미지");
            _breakerDamage = BindStat(Refs.BreakerDamage_Button, Refs.BreakerDamage_Text, "브레이커 대미지");
            _breakerCritDamage = BindStat(Refs.BreakerCritDamage_Button, Refs.BreakerCritDamage_Text, "브레이커 치명타 대미지");
            _breakerClicks = BindStat(Refs.BreakerClicks_Button, Refs.BreakerClicks_Text, "브레이커 클릭 수");
            _destroyedStars = BindStat(Refs.DestroyedStars_Button, Refs.DestroyedStars_Text, "파괴된 별");
            _destroyedPlanets = BindStat(Refs.DestroyedPlanets_Button, Refs.DestroyedPlanets_Text, "파괴된 행성");
            _collectedMoons = BindStat(Refs.CollectedMoons_Button, Refs.CollectedMoons_Text, "수집한 달");
            _collectedComets = BindStat(Refs.CollectedComets_Button, Refs.CollectedComets_Text, "수집한 혜성");
            _destroyedAsteroids = BindStat(Refs.DestroyedAsteroids_Button, Refs.DestroyedAsteroids_Text, "파괴된 소행성");
            _addedTime = BindStat(Refs.AddedTime_Button, Refs.AddedTime_Text, "추가된 시간");
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

        private StatCell BindStat(Refs button, Refs value, string label)
        {
            StatCell cell = new(View.Button(button), View.Text(value), label);

            BindEvent(cell.Button, _ => HoverStat(cell), ETouchEvent.PointerEnter);
            BindEvent(cell.Button, _ => LeaveStat(cell), ETouchEvent.PointerExit);
            BindEvent(cell.Button, _ => cell.Animation.Press(), ETouchEvent.PointerDown);
            BindEvent(cell.Button, _ => cell.Animation.Release(), ETouchEvent.PointerUp);

            return cell;
        }

        private void HoverStat(StatCell cell)
        {
            cell.Animation.Hover();
            ShowTooltip(cell.Button, cell.Label);
        }

        private void LeaveStat(StatCell cell)
        {
            cell.Animation.Leave();
            HideTooltip(cell.Button);
        }

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
                    : 1f;

                _stageFillTarget = fill;
                _stageFillTimer = 0f;
                _isShowingStage = true;

                // SoundManager인스턴스가 존재하면 Slider SFX 재생 길이를 반환
                // Slider SFX 길이만큼 슬라이더가 올라가는 속도를 맞추기 위함
                float length = SoundManager.Instance != null ? SoundManager.Instance.SliderLength : 0f;
                _stageFillDuration = length > 0f ? length : DefaultDuration;
                _matterDuration = _stageFillDuration / MatterStepCount;
                SoundManager.Instance?.PlaySlider();
                //_stageFill.anchorMax = new Vector2(0f, _stageFill.anchorMax.y);
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

        // earned: 이 판이 번 Gold. settled: 결산이 더한 Gold(이정표로 끝났으면 목표 잔액까지의 차액). total: 결산 뒤 진행 상태의 Gold.
        public void ShowGold(long earned, long settled, bool milestone, long total)
        {
            _earnedTarget = milestone ? settled : earned;
            _totalTarget = total;
            // 결산 전 잔액. 실제로 더해진 것은 settled다(이정표 판은 번 Gold를 버린다).
            _totalBegin = total - settled;

            if (_totalLabel != null)
                _totalLabel.text = milestone ? "REWARD" : "TOTAL";

            if (_earned != null)
                _earned.text = Money(0);

            if (_totalGold != null)
                _totalGold.text = Money(_totalBegin);
        }

        // 오른쪽 통계 칸: 스킬별 피해·수집 통계(판 기록)와 종류별 처치 수(물질 행과 같은 값). 증가 연출 없이 바로 보인다.
        public void ShowStats(BattleStats stats, int asteroids, int planets, int stars)
        {
            ShowStat(_electricStarDamage, NumberText.Compact(stats.ElectricStarDamage));
            ShowStat(_laserDamage, NumberText.Compact(stats.LaserDamage));
            ShowStat(_supernovaDamage, NumberText.Compact(stats.SupernovaDamage));
            ShowStat(_goldenAsteroidGold, "$" + NumberText.Compact(stats.GoldenAsteroidGold));
            ShowStat(_electricAsteroidDamage, NumberText.Compact(stats.ElectricAsteroidDamage));
            ShowStat(_breakerDamage, NumberText.Compact(stats.BreakerDamage));
            ShowStat(_breakerCritDamage, NumberText.Compact(stats.BreakerCriticalDamage));
            ShowStat(_breakerClicks, Whole(stats.BreakerTicks));
            ShowStat(_destroyedStars, Whole(stars));
            ShowStat(_destroyedPlanets, Whole(planets));
            ShowStat(_collectedMoons, Whole(stats.CollectedMoons));
            ShowStat(_collectedComets, Whole(stats.CollectedComets));
            ShowStat(_destroyedAsteroids, Whole(asteroids));
            ShowStat(_addedTime, stats.AddedSeconds.ToString("0.#", CultureInfo.InvariantCulture));
        }

        private static void ShowStat(StatCell cell, string value)
        {
            if (cell.Value != null)
                cell.Value.text = value;
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

        private static string Whole(int count) => count.ToString(CultureInfo.InvariantCulture);

        private static string Money(long gold) =>
            "$" + gold.ToString("N0", CultureInfo.InvariantCulture);

        // MonoBehavior
        private void Update()
        {
            if (_isShowingStage)
                ShowingStage();

            if (_isShowMatter)
                ShowMatter();

            UpdateScaleEffect();
        }

        /// <summary>
        /// 결과값 증가하는 연출 메서드
        /// </summary>
        private void ShowingStage()
        {
            _stageFillTimer += Time.deltaTime;

            float t = Mathf.Clamp01(_stageFillTimer / _stageFillDuration);

            // 부드럽게 증가
            t = Mathf.SmoothStep(0f, 1f, t);

            float currentFill =
                Mathf.Lerp(0f, _stageFillTarget, t);

            if (_stageFill != null)
                _stageFill.anchorMax = new Vector2(currentFill, _stageFill.anchorMax.y);

            if (t >= 1f)
            {
                _isShowingStage = false;

                // 최종값 보정
                if (_stageFill != null)
                    _stageFill.anchorMax = new Vector2(_stageFillTarget, _stageFill.anchorMax.y);
            }
        }
        private void ShowMatter()
        {
            _matterTimer += Time.deltaTime;

            float t = Mathf.Clamp01(_matterTimer / _matterDuration);

            // 부드럽게 증가
            t = Mathf.SmoothStep(0f, 1f, t);

            // 순서대로 증가 연출
            switch (_matterType)
            {
                case MatterType.Asteroid:
                    IncreaseResult(_asteroidText, _asteroidTarget, t); break;
                case MatterType.Planet:
                    IncreaseResult(_planetText, _planetTarget, t); break;
                case MatterType.Star:
                    IncreaseResult(_starText, _starTarget, t); break;
                case MatterType.Earned:
                    IncreaseResult(_earned, _earnedTarget, t); break;
                case MatterType.Total:
                    IncreaseResult(_totalGold, _totalTarget, t); break;
            }
        }

        /// <summary>
        /// 각 텍스트 UI를 0부터 현재 값까지 증가
        /// </summary>
        /// <param name="_text">TMP_Text UI</param>
        /// <param name="_target">각 결과값</param>
        /// <param name="t">lerp 타이머</param>
        private void IncreaseResult(TMP_Text _text, int _target, float t)
        {
            // 값이 있을때만 증가 연출
            // (그렇지 않으면 0도 증가하는 연출이 발생해서 기다리는데 지장이 있다)
            //if (_target == 0) t = 1f;

            int value = IncreaseLerp(_target, t);
            if (_text != null)
            {
                _text.text = Count(value);

                if (!_previousKills.TryGetValue(_text, out int previous) || previous != value)
                {
                    PlayScaleEffect(_text);
                    _previousKills[_text] = value;
                }
            }

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
                        _matterType = MatterType.Earned; break;
                }
            }
        }
        private void IncreaseResult(TMP_Text _text, long _target, float t)
        {
            // 값이 있을때만 증가 연출
            // (그렇지 않으면 0도 증가하는 연출이 발생해서 기다리는데 지장이 있다)
            //if (_target == 0) t = 1f;

            // Total은 이번판에 얻은 Gold를 더해서 결산
            long value = _matterType.Equals(MatterType.Total) ?
                LerpLong(_totalBegin, _target, t) : LerpLong(_target, t);
            if (_text != null)
            {
                _text.text = Money(value);

                if (!_previousGolds.TryGetValue(_text, out long previous) || previous != value)
                {
                    PlayScaleEffect(_text);
                    _previousGolds[_text] = value;
                }
            }

            if (t >= 1f)
            {
                _text.text = Money(_target);

                _matterTimer = 0f;

                switch (_matterType)
                {
                    case MatterType.Earned:
                        _matterType = MatterType.Total; break;
                    case MatterType.Total:
                        _matterType = MatterType.Done;
                        _isShowMatter = false;
                        SoundManager.Instance?.PlayClosing(); break;
                }
            }
        }

        /// <summary>
        /// 부드러운 효과
        /// </summary>
        /// <param name="_target">설정값</param>
        /// <param name="t">타이머</param>
        /// <returns>0~100% 값</returns>
        private int IncreaseLerp(int _target, float t) => Mathf.RoundToInt(Mathf.Lerp(0, _target, t));
        private long LerpLong(long target, float t) => (long)((double)target * t);
        private long LerpLong(long start, long target, float t) => start + (long)((double)(target - start) * t);

        // 확대 효과
        private void PlayScaleEffect(TMP_Text text)
        {
            // 기존 효과가 남아있으면 원상복구
            if (_scaleTarget != null)
                _scaleTarget.transform.localScale = Vector3.one;

            _scaleTarget = text;
            _scaleTimer = 0f;
            _isScaleEffect = true;
        }
        private void UpdateScaleEffect()
        {
            if (!_isScaleEffect || _scaleTarget == null) return;

            _scaleTimer += Time.deltaTime;

            float t = _scaleTimer / ScaleDuration;

            if (t <= 0.5f)
            {
                float scaleT = t * 2f;

                _scaleTarget.transform.localScale =
                    Vector3.Lerp(
                        Vector3.one, Vector3.one * HighlightScale,
                        scaleT);
            }
            else
            {
                float scaleT = (t - 0.5f) * 2f;

                _scaleTarget.transform.localScale =
                    Vector3.Lerp(
                        Vector3.one * HighlightScale, Vector3.one,
                        scaleT);
            }

            if (t >= 1f)
            {
                _scaleTarget = null;
                _isScaleEffect = false;
            }
        }
    }
}
