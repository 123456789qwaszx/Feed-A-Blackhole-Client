using UnityEngine;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    // 전투 화면의 이정표 누적 표시: Level 막대 아래 마름모 10칸. 이번 구간(판 시작 Level → 목표 Level)에서 오른 Level 수만큼 채운다.
    // 부분 EXP는 막대만 보여 준다 — 칸은 완전히 비거나 완전히 찬다. 숫자·글자는 없다.
    // 프리팹 구조: 행(root) 아래 칸 10개, 칸마다 자식 0 = 빈 윤곽 Image, 자식 1 = 채운 Image. 색은 프리팹의 Image 색을 기준으로 쓴다.
    // 칸은 TextBindings·ImageBindings에 없어 Theme가 건드리지 않는다. 크기·색은 여기서만 바꾼다.
    // - 처음 보일 때(Show의 첫 호출)는 이미 오른 Level을 연출 없이 맞춘다. 그 뒤 늘어난 칸만 짧게 커졌다 돌아온다(여럿이면 조금씩 늦춰서).
    // - 9칸이면 마지막 빈 칸만 천천히 맥동한다.
    // - 10칸(이정표 도달): Celebrate → 초록 10칸을 잠깐 보인 뒤 한 줄을 금색으로 바꾸며 살짝 키우고 파편을 낸다.
    //   IsCelebrating이 끝날 때까지 GameHost가 결산 전환을 미룬다. 연출은 실제 시간으로 흐른다(판은 이미 끝나 있다).
    // Reset은 색·크기·연출을 모두 되돌린다. 화면이 닫히거나 새 판이 시작될 때 부른다.
    internal sealed class MilestoneRow
    {
        public const int Slots = 10;

        // 연출 조절값. 화면에서 보고 맞춘다.
        private const float PopDuration = 0.18f;   // 한 칸 완성: 1 → PopScale → 1
        private const float PopScale = 1.18f;
        private const float PopStagger = 0.04f;    // 한 프레임에 여러 칸이 차면 이만큼씩 늦춰 터뜨린다
        private const float PulsePeriod = 1f;      // 마지막 빈 칸의 맥동 주기(초)
        private const float PulseScale = 0.06f;    // 1 ± 이 값
        private const float PulseAlphaMin = 0.55f;
        private const float CelebrateHold = 0.12f; // 초록 10칸을 보여 주는 시간
        private const float GoldPopDuration = 0.2f; // 금색 전환과 함께 한 줄이 커졌다 돌아오는 시간
        private const float GoldPopScale = 1.12f;
        private static readonly Color Gold = new Color32(0xE9, 0xB6, 0x3C, 0xFF);

        private struct Token
        {
            public RectTransform Rect;
            public Image Empty;
            public Image Filled;
            public Color EmptyColor;
            public Color FilledColor;
            public float PopAt; // 이 시각부터 터진다. NaN이면 없음.
        }

        private enum Phase { None, Hold, Gold, Done }

        private readonly RectTransform _root;
        private readonly UIMilestoneBurst _burst;
        private readonly Token[] _tokens;
        private float _time;
        private int _completed = -1;
        private Phase _phase;
        private float _phaseStart;

        public bool IsCelebrating => _phase == Phase.Hold || _phase == Phase.Gold;

        // root: 칸 10개를 자식으로 가진 행. burst: 파편(없어도 된다).
        public MilestoneRow(RectTransform root, UIMilestoneBurst burst)
        {
            _root = root;
            _burst = burst;

            int count = root != null ? Mathf.Min(root.childCount, Slots) : 0;
            _tokens = new Token[count];

            for (int i = 0; i < count; i++)
            {
                var rect = (RectTransform)root.GetChild(i);
                Image empty = rect.childCount > 0 ? rect.GetChild(0).GetComponent<Image>() : null;
                Image filled = rect.childCount > 1 ? rect.GetChild(1).GetComponent<Image>() : null;

                _tokens[i] = new Token
                {
                    Rect = rect,
                    Empty = empty,
                    Filled = filled,
                    EmptyColor = empty != null ? empty.color : Color.white,
                    FilledColor = filled != null ? filled.color : Color.white,
                    PopAt = float.NaN,
                };

                if (empty != null) empty.raycastTarget = false;
                if (filled != null) filled.raycastTarget = false;
            }

            if (root != null && count != Slots)
                Debug.LogWarning($"[이정표] 행에 칸이 {Slots}개여야 한다. 지금 {root.childCount}개.", root);
        }

        public void SetVisible(bool visible)
        {
            if (_root != null && _root.gameObject.activeSelf != visible)
                _root.gameObject.SetActive(visible);
        }

        // 완료한 칸 수를 맞춘다. 처음(Reset 뒤)이면 연출 없이, 그 뒤 늘어난 칸은 터뜨린다. 연출 중에는 바뀌지 않는다(판이 끝나 있다).
        public void Show(int completed)
        {
            completed = Mathf.Clamp(completed, 0, _tokens.Length);

            if (completed == _completed || _phase != Phase.None)
                return;

            bool first = _completed < 0;
            int previous = Mathf.Max(0, _completed);
            _completed = completed;

            for (int i = 0; i < _tokens.Length; i++)
            {
                ref Token token = ref _tokens[i];
                bool filled = i < completed;
                SetFilled(ref token, filled, filled ? token.FilledColor : token.EmptyColor);
                token.PopAt = !first && filled && i >= previous ? _time + (i - previous) * PopStagger : float.NaN;
                token.Rect.localScale = Vector3.one;
            }
        }

        // 이정표 달성 연출을 시작한다. 이미 시작했거나 칸이 없으면 false. 한 판에 한 번만 부른다(GameHost).
        public bool TryCelebrate()
        {
            if (_phase != Phase.None || _tokens.Length == 0)
                return false;

            // 10칸이 모두 찬 모습을 먼저 보인다(Show가 이미 맞췄더라도 확실히).
            for (int i = 0; i < _tokens.Length; i++)
            {
                ref Token token = ref _tokens[i];
                SetFilled(ref token, true, token.FilledColor);
                token.PopAt = float.NaN;
                token.Rect.localScale = Vector3.one;
            }

            _completed = _tokens.Length;
            _phase = Phase.Hold;
            _phaseStart = _time;
            return true;
        }

        // 매 프레임, 실제 시간으로.
        public void Tick(float deltaTime)
        {
            _time += deltaTime;

            switch (_phase)
            {
                case Phase.None:
                    TickPops();
                    TickPulse();
                    break;

                case Phase.Hold:
                    if (_time - _phaseStart >= CelebrateHold)
                        BeginGold();
                    break;

                case Phase.Gold:
                    TickGold();
                    break;
            }
        }

        public void Reset()
        {
            _completed = -1;
            _phase = Phase.None;
            _phaseStart = 0;

            for (int i = 0; i < _tokens.Length; i++)
            {
                ref Token token = ref _tokens[i];
                SetFilled(ref token, false, token.EmptyColor);
                token.PopAt = float.NaN;
                token.Rect.localScale = Vector3.one;
            }

            if (_burst != null)
                _burst.StopAndClear();
        }

        private void TickPops()
        {
            for (int i = 0; i < _tokens.Length; i++)
            {
                ref Token token = ref _tokens[i];

                if (float.IsNaN(token.PopAt) || _time < token.PopAt)
                    continue;

                float t = (_time - token.PopAt) / PopDuration;

                if (t >= 1)
                {
                    token.PopAt = float.NaN;
                    token.Rect.localScale = Vector3.one;
                    continue;
                }

                float scale = 1 + (PopScale - 1) * Mathf.Sin(t * Mathf.PI);
                token.Rect.localScale = new Vector3(scale, scale, 1);
            }
        }

        // 9칸이 찼을 때 마지막 빈 칸만 숨 쉬듯 커졌다 작아지고 옅어졌다 짙어진다.
        private void TickPulse()
        {
            if (_tokens.Length != Slots)
                return;

            ref Token last = ref _tokens[Slots - 1];
            bool pulsing = _completed == Slots - 1 && float.IsNaN(last.PopAt);

            if (!pulsing)
                return;

            float wave = 0.5f + 0.5f * Mathf.Sin(_time / PulsePeriod * Mathf.PI * 2);
            float scale = 1 + PulseScale * (wave * 2 - 1);
            last.Rect.localScale = new Vector3(scale, scale, 1);

            if (last.Empty != null)
            {
                Color color = last.EmptyColor;
                color.a *= Mathf.Lerp(PulseAlphaMin, 1, wave);
                last.Empty.color = color;
            }
        }

        private void BeginGold()
        {
            _phase = Phase.Gold;
            _phaseStart = _time;

            for (int i = 0; i < _tokens.Length; i++)
                SetFilled(ref _tokens[i], true, Gold);

            if (_burst != null)
                _burst.Play();
        }

        private void TickGold()
        {
            float since = _time - _phaseStart;
            float t = Mathf.Clamp01(since / GoldPopDuration);
            float scale = t < 1 ? 1 + (GoldPopScale - 1) * Mathf.Sin(t * Mathf.PI) : 1;

            for (int i = 0; i < _tokens.Length; i++)
                _tokens[i].Rect.localScale = new Vector3(scale, scale, 1);

            float length = Mathf.Max(GoldPopDuration, _burst != null ? _burst.Duration : 0);

            if (since >= length)
                _phase = Phase.Done; // 금색 10칸은 화면이 닫힐 때까지 그대로 둔다
        }

        private static void SetFilled(ref Token token, bool filled, Color color)
        {
            if (token.Empty != null)
            {
                token.Empty.enabled = !filled;
                token.Empty.color = token.EmptyColor;
            }

            if (token.Filled != null)
            {
                token.Filled.enabled = filled;
                token.Filled.color = color;
            }
        }
    }
}
