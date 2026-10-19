using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    // 전투 화면에 보이는 은하 한 마리의 상태. GameHost가 판의 은하를 찾아 매 프레임 넘긴다. 은하가 없는 판이면 넘기지 않는다(null).
    // Key는 은하마다 다른 값(적 번호)이라, 바뀌면 새 은하로 보고 막대를 처음부터 연다.
    internal readonly struct GalaxyHealthState
    {
        public readonly int Key;
        public readonly int Tier;
        public readonly float Health;
        public readonly float MaxHealth;
        public readonly Color Color;

        public GalaxyHealthState(int key, int tier, float health, float maxHealth, Color color)
        {
            Key = key;
            Tier = tier;
            Health = health;
            MaxHealth = maxHealth;
            Color = color;
        }

        public float Fraction => MaxHealth > 0 ? Mathf.Clamp01(Health / MaxHealth) : 0;
        public bool IsDead => Health <= 0;
    }

    // 은하 체력바 [임시: 프리팹 없이 코드로 만든다] — 화면 아래 가운데. 이름(색) · 남은 체력 / 최대 체력, 그 아래 막대.
    // - 막대: 은하 색으로 채운다. 깎인 몫은 흰 꼬리(trail)가 잠깐 남았다가 따라 줄어든다. 맞은 순간 막대가 잠깐 밝아진다.
    // - 나타날 때 아래에서 살짝 떠오르며 보이고, 은하가 죽으면 0까지 줄어든 뒤 잠시 있다가 흐려진다.
    // - 판(Session)을 모른다. BattleScreen이 상태(GalaxyHealthState)를 넘기고 Tick으로 연출을 흘린다(실제 시간).
    // - 글꼴·글자 크기·글자 색은 StyledTexts가, 바탕·막대 홈 색은 ThemedImages가 테마에 맞춘다. 터치를 받지 않는다.
    internal sealed class GalaxyHealthBar
    {
        // 배치(기준 해상도 1920×1080, Safe Area 안). 화면에서 보고 맞출 값이다.
        private const float Width = 880f;
        private const float Height = 88f;
        private const float BottomMargin = 36f;
        private const float Padding = 28f;
        private const float TrackHeight = 16f;
        private const float TrackBottom = 18f;
        private const float NameSize = 30f;
        private const float HealthSize = 26f;

        // 연출(초). 실제 시간으로 흐른다.
        private const float AppearSeconds = 0.3f;
        private const float AppearRise = 24f;
        private const float TrailHoldSeconds = 0.35f;
        private const float TrailSharpness = 6f;
        private const float FlashSeconds = 0.12f;
        private const float FlashAmount = 0.55f;
        private const float DeathHoldSeconds = 0.6f;
        private const float FadeSeconds = 0.4f;

        private static readonly Color TrailColor = new(1f, 1f, 1f, 0.85f);

        private readonly RectTransform _root;
        private readonly CanvasGroup _group;
        private readonly RectTransform _fill;
        private readonly Image _fillImage;
        private readonly RectTransform _trail;
        private readonly TMP_Text _name;
        private readonly TMP_Text _health;

        private int _key = int.MinValue;
        private float _shownHealth = float.NaN;
        private float _fraction;
        private float _trailFraction;
        private float _sinceHit = float.PositiveInfinity;
        private float _appear;
        private float _sinceDeath = float.NaN;
        private bool _active;
        private Color _color = Color.white;

        public GalaxyHealthBar(RectTransform parent, TMP_FontAsset font, StyledTexts texts, ThemedImages images)
        {
            _root = NewRect("Galaxy Health", parent);
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0f);
            _root.pivot = new Vector2(0.5f, 0f);
            _root.sizeDelta = new Vector2(Width, Height);
            _root.anchoredPosition = new Vector2(0, BottomMargin);

            _group = _root.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _group.alpha = 0;

            Image panel = NewImage("Panel", _root, new Color(1, 1, 1, 0.82f));
            Stretch(panel.rectTransform);
            images.AddColor(panel, UIColorRole.Surface);

            RectTransform track = NewRect("Track", _root);
            track.anchorMin = new Vector2(0, 0);
            track.anchorMax = new Vector2(1, 0);
            track.pivot = new Vector2(0.5f, 0);
            track.offsetMin = new Vector2(Padding, TrackBottom);
            track.offsetMax = new Vector2(-Padding, TrackBottom + TrackHeight);
            Image trackImage = track.gameObject.AddComponent<Image>();
            trackImage.raycastTarget = false;
            trackImage.color = new Color(1, 1, 1, 0.35f);
            images.AddColor(trackImage, UIColorRole.Line);

            _trail = NewImage("Trail", track, TrailColor).rectTransform;
            Stretch(_trail);
            _fillImage = NewImage("Fill", track, Color.white);
            _fill = _fillImage.rectTransform;
            Stretch(_fill);

            _name = NewText("Name", _root, font, NameSize, TextAlignmentOptions.BottomLeft);
            _health = NewText("Health", _root, font, HealthSize, TextAlignmentOptions.BottomRight);
            texts.Add(_name, UITextRole.Body);
            texts.Add(_health, UITextRole.Caption);

            _root.gameObject.SetActive(false);
        }

        // 이번 프레임의 은하 상태. 은하가 없으면 null — 보이던 막대는 바로 숨긴다(새 판·은하 없는 판).
        // name: 지금 언어의 은하 이름(색). 은하가 바뀌거나 언어가 바뀔 때만 다시 쓴다.
        public void Show(GalaxyHealthState? state, string name)
        {
            if (state is not GalaxyHealthState galaxy)
            {
                Hide();
                return;
            }

            if (galaxy.Key != _key)
                Open(galaxy, name);

            if (galaxy.Health != _shownHealth)
            {
                if (!float.IsNaN(_shownHealth) && galaxy.Health < _shownHealth)
                    _sinceHit = 0;

                _shownHealth = galaxy.Health;
                _fraction = galaxy.Fraction;
                _health.text = HealthText(galaxy);

                if (galaxy.IsDead && float.IsNaN(_sinceDeath))
                    _sinceDeath = 0;
            }
        }

        // 언어가 바뀌었다. 보이는 이름을 다시 쓴다.
        public void Rename(string name)
        {
            if (_active)
                _name.text = name;
        }

        public void Hide()
        {
            if (!_active)
                return;

            _active = false;
            _key = int.MinValue;
            _root.gameObject.SetActive(false);
        }

        // 연출을 흘린다(실제 시간).
        public void Tick(float delta)
        {
            if (!_active)
                return;

            _appear = Mathf.Min(1, _appear + delta / AppearSeconds);
            float ease = 1 - (1 - _appear) * (1 - _appear);
            _root.anchoredPosition = new Vector2(0, BottomMargin - AppearRise * (1 - ease));

            // 꼬리: 맞은 뒤 잠깐 머물렀다가 막대를 따라 줄어든다. 막대보다 짧아지지 않는다.
            _sinceHit += delta;

            if (_sinceHit >= TrailHoldSeconds)
                _trailFraction = Mathf.Lerp(_trailFraction, _fraction, 1 - Mathf.Exp(-TrailSharpness * delta));

            _trailFraction = Mathf.Max(_trailFraction, _fraction);
            SetWidth(_fill, _fraction);
            SetWidth(_trail, _trailFraction);

            float flash = _sinceHit < FlashSeconds ? FlashAmount * (1 - _sinceHit / FlashSeconds) : 0;
            _fillImage.color = Color.Lerp(_color, Color.white, flash);

            float alpha = ease;

            if (!float.IsNaN(_sinceDeath))
            {
                _sinceDeath += delta;
                alpha *= 1 - Mathf.Clamp01((_sinceDeath - DeathHoldSeconds) / FadeSeconds);

                if (_sinceDeath >= DeathHoldSeconds + FadeSeconds)
                {
                    _group.alpha = 0;
                    return;
                }
            }

            _group.alpha = alpha;
        }

        private void Open(in GalaxyHealthState galaxy, string name)
        {
            _key = galaxy.Key;
            _color = galaxy.Color;
            _color.a = 1;
            _shownHealth = float.NaN;
            _fraction = galaxy.Fraction;
            _trailFraction = _fraction;
            _sinceHit = float.PositiveInfinity;
            _sinceDeath = float.NaN;
            _appear = 0;
            _name.text = name;
            _active = true;
            _group.alpha = 0;
            _root.gameObject.SetActive(true);
        }

        private static string HealthText(in GalaxyHealthState galaxy) =>
            NumberText.Compact((double)Mathf.Max(0, Mathf.Ceil(galaxy.Health))) + " / " + NumberText.Compact((double)galaxy.MaxHealth);

        private static void SetWidth(RectTransform rect, float fraction)
        {
            Vector2 max = rect.anchorMax;

            if (Mathf.Approximately(max.x, fraction))
                return;

            max.x = fraction;
            rect.anchorMax = max;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            Image image = NewRect(name, parent).gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.color = color;
            return image;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static TMP_Text NewText(string name, RectTransform parent, TMP_FontAsset font, float size, TextAlignmentOptions alignment)
        {
            RectTransform rect = NewRect(name, parent);
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 1);
            rect.offsetMin = new Vector2(Padding, TrackBottom + TrackHeight + 4);
            rect.offsetMax = new Vector2(-Padding, -6);

            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = alignment;
            text.fontSize = size;
            text.color = Color.white;

            if (font != null)
                text.font = font;

            return text;
        }
    }
}
