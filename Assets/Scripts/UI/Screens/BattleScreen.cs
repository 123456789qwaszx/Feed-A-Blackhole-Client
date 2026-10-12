using System;
using System.Globalization;
using BlackHole.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BlackHole.Unity
{
    // 전투 화면(전투 장면 위의 겹 화면). 전투 중에 필요한 정보만 보여 준다: 남은 시간, 다음 Level까지의 진행 막대, 일시정지·종료 버튼.
    // Level 숫자·목표·% 글자는 두지 않는다(막대와 겹친다). 번 Gold 칩은 프리팹에서 꺼 두었다(결산에서 본다) — 켜면 다시 보인다.
    // 전투 Session을 모른다 — GameHost가 전투 Step 뒤 표시 값을 넘긴다. 진행 중인 판이 없으면 비어 있는 표시(ShowIdle)다.
    // 번 Gold는 이 판의 합계일 뿐이다. 진행 상태의 Gold는 판이 끝난 뒤 결산이 바꾼다.
    public sealed class BattleScreen : UIRoot<BattleScreen.Refs>, ILocalizedView
    {
        public enum Refs
        {
            Remaining_Text,
            // 번 Gold. 칩(GoldChip_Image)이 프리팹에서 꺼져 있어 지금은 보이지 않는다.
            EarnedGold_Text,
            // 다음 Level까지의 몫만큼 늘어나는 막대. 폭은 anchorMax.x로 정한다.
            LevelFill_Image,
            PauseBtn_Button,
            PauseBtn_Text,
            EndBtn_Button,
            EndBtn_Text,
            // 노치·둥근 모서리를 피하는 영역. 화면을 열 때와 해상도가 바뀔 때 UIManager가 Safe Area에 맞춘다(SafeAreaUtility).
            SafeAreaRoot,

            // Presentation이 바꾸는 그림. 코드는 건드리지 않는다.
            // 배경이 어두워지면 Image Theme로 칩·아이콘을 바꾼다(예: 아이콘 ink → ivory).
            GoldChip_Image,
            GoldIcon_Image,
            LevelChip_Image,
            LevelBar_Image,
            PauseBtn_Image,
            PauseIcon_Image,
            EndBtn_Image,
            EndIcon_Image,
        }

        public event Action PauseClicked;
        public event Action EndClicked;

        private TMP_Text _remaining;
        private TMP_Text _earned;
        private RectTransform _levelFill;
        private TMP_Text _pauseLabel;
        private int _shownTenths = -1;
        private long _shownEarned = -1;
        private bool _idle;
        private bool? _shownPaused;
        private Localizer _localizer;

        private const float FillSharpness = 5f;
        // 남은 시간 숫자 한 칸의 폭(글자 크기 기준)과 소수 부분의 크기. 글꼴이 바뀌면 화면에서 간격을 보고 맞춘다.
        private const string DigitWidth = "0.6em";
        private const string FractionSize = "70%";
        private float _fillValue = float.NaN;

        protected override void OnInitialize()
        {
            ScreenRefs.WarnMissing<Refs>(this);

            _remaining = View.Text(Refs.Remaining_Text);
            _earned = View.Text(Refs.EarnedGold_Text);
            _levelFill = View.Rect(Refs.LevelFill_Image);
            _pauseLabel = View.Text(Refs.PauseBtn_Text);

            BindEvent(View.Button(Refs.PauseBtn_Button), HandlePauseClicked);
            BindEvent(View.Button(Refs.EndBtn_Button), HandleEndClicked);
        }

        private void HandlePauseClicked(PointerEventData _) => PauseClicked?.Invoke();
        private void HandleEndClicked(PointerEventData _) => EndClicked?.Invoke();

        // 진행 중인 판이 없을 때의 표시. 매 프레임 불러도 된다.
        public void ShowIdle()
        {
            if (_shownTenths != int.MinValue && _remaining != null)
            {
                _shownTenths = int.MinValue;
                _remaining.text = "-";
            }

            if (_shownEarned != long.MinValue && _earned != null)
            {
                _shownEarned = long.MinValue;
                _earned.text = string.Empty;
            }

            if (!_idle)
            {
                _idle = true;
                _fillValue = float.NaN;
                SetLevelFill(0);
            }

            if (_shownPaused != false && _pauseLabel != null)
            {
                _shownPaused = false;
                _pauseLabel.text = PauseLabel(false);
            }
        }

        // 매 프레임 불러도 된다. 보이는 값이 바뀔 때만 글자를 고친다.
        // level·progress: 지금 Level과 다음 Level까지의 몫(0 ~ 1, 마지막 Level이면 1). 막대만 그린다 — Level이 오르면 막대가 한 바퀴 돈다.
        public void Show(float remainingSeconds, long earnedGold, bool paused, int level, float progress)
        {
            _idle = false;

            int tenths = Mathf.CeilToInt(remainingSeconds * 10);

            if (tenths != _shownTenths && _remaining != null)
            {
                _shownTenths = tenths;
                _remaining.text = RemainingText(tenths);
            }

            if (earnedGold != _shownEarned && _earned != null)
            {
                _shownEarned = earnedGold;
                _earned.text = EarnedText(earnedGold);
            }

            UpdateLevelFill(level, progress);

            if (paused != _shownPaused && _pauseLabel != null)
            {
                _shownPaused = paused;
                _pauseLabel.text = PauseLabel(paused);
            }
        }

        // 지금 보이는 값(번 Gold·일시정지 글자)을 지금 언어로 다시 쓴다. 비어 있는 표시는 그대로 둔다.
        public void Relocalize(Localizer localizer)
        {
            _localizer = localizer;

            if (_earned != null && _shownEarned >= 0)
                _earned.text = EarnedText(_shownEarned);

            if (_pauseLabel != null && _shownPaused is bool paused)
                _pauseLabel.text = PauseLabel(paused);
        }

        private string EarnedText(long earnedGold) => Localized("battle.earned", OverThousand.GoldThousand(earnedGold));

        // 남은 시간 글자. 0.1초마다 바뀌므로 흔들림을 줄인다.
        // - 숫자는 칸 폭을 고정한다(<mspace>). 1과 8의 폭이 달라 가운데 정렬에서 숫자 묶음이 좌우로 움직이지 않게 한다.
        //   소수점은 칸에 넣지 않는다 — 숫자 한 칸만큼 벌어져 보인다.
        // - 소수 부분(".9")은 작게 그려 자주 바뀌는 자리가 덜 눈에 띄게 한다.
        // 자릿수가 바뀔 때(10 → 9)만 묶음 폭이 한 칸 줄어든다.
        private static string RemainingText(int tenths)
        {
            tenths = Mathf.Max(0, tenths);
            string whole = (tenths / 10).ToString(CultureInfo.InvariantCulture);
            string fraction = (tenths % 10).ToString(CultureInfo.InvariantCulture);

            return $"<mspace={DigitWidth}>{whole}</mspace><size={FractionSize}>.<mspace={DigitWidth}>{fraction}</mspace></size>";
        }

        private string PauseLabel(bool paused) => Localized(paused ? "battle.resume" : "battle.pause");

        private string Localized(string key, params object[] args) =>
            _localizer != null ? _localizer.Format(key, args) : key;

        private void SetLevelFill(float amount)
        {
            // if (_levelFill != null)
            //     _levelFill.anchorMax = new Vector2(Mathf.Clamp01(amount), _levelFill.anchorMax.y);
            if (_levelFill == null)
                return;
            
            float x = Mathf.Clamp01(amount);

            if (Mathf.Approximately(_levelFill.anchorMax.x, x))
                return;
            
            _levelFill.anchorMax = new Vector2(x, _levelFill.anchorMax.y);
        }

        private void UpdateLevelFill(int level, float progress)
        {
            float target = level + Mathf.Min(Mathf.Clamp01(progress), 0.9999f);

            if (float.IsNaN(_fillValue) || target < _fillValue)
            {
                _fillValue = target;
            }
            else
            {
                float t = 1f - Mathf.Exp(-FillSharpness * Time.unscaledDeltaTime);
                _fillValue = Mathf.Lerp(_fillValue, target, t);

                if (target - _fillValue < 0.001f)
                    _fillValue = target;
            }

            SetLevelFill(_fillValue - Mathf.Floor(_fillValue));
        }
    }
}
