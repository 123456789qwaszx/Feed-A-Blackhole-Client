using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    // 서버가 기록을 받아들이지 않았을 때 화면 위쪽에 잠깐 띄우는 알림 글자(W3-5, 계약 5.9). 프리팹 없이 코드로 만든다.
    // 나타남 → 머묾 → 사라짐 순서로 혼자 끝나고, 입력을 가로채지 않는다. 실제 시간으로 흐른다(timeScale과 무관).
    // 새로 Show하면 지금 것을 처음부터 다시 보인다. 글자는 호출하는 쪽이 번역해서 넘긴다.
    [DisallowMultipleComponent]
    internal sealed class RejectionNotice : MonoBehaviour
    {
        private const float FadeInSeconds = 0.3f;
        private const float HoldSeconds = 5f;
        private const float FadeOutSeconds = 0.8f;

        // 배치(기준 해상도 1920×1080).
        private const float Width = 1500f;
        // 업그레이드 화면 위쪽의 Gold 표시·구역 이름 아래, 노드 트리 위에 놓는다. 타이틀에서는 로고 위다.
        private const float Height = 80f;
        private const float TopMargin = 125f;
        private const float TextSize = 30f;
        private const float TextPadding = 28f;

        private static readonly Color PanelColor = new(0.04f, 0.03f, 0.08f, 0.82f);
        private static readonly Color TextColor = new(1f, 0.9f, 0.82f, 1f);

        private CanvasGroup _group;
        private TMP_Text _text;
        private float _elapsed = -1f;

        public bool IsShowing => _elapsed >= 0f;

        public static RejectionNotice Create(Transform canvas, int siblingIndex, TMP_FontAsset font, float scale)
        {
            if (canvas == null)
                throw new ArgumentNullException(nameof(canvas));

            scale = scale > 0 ? scale : 1;

            var view = new GameObject("Rejection Notice", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            view.layer = canvas.gameObject.layer;
            var rect = (RectTransform)view.transform;
            rect.SetParent(canvas, false);
            rect.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, canvas.childCount - 1));
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -TopMargin * scale);
            rect.sizeDelta = new Vector2(Width * scale, Height * scale);

            var panel = view.GetComponent<Image>();
            panel.color = PanelColor;
            panel.raycastTarget = false;

            var textObject = new GameObject("Text", typeof(RectTransform));
            textObject.layer = view.layer;
            var textRect = (RectTransform)textObject.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(TextPadding * scale, 0f);
            textRect.offsetMax = new Vector2(-TextPadding * scale, 0f);

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = TextSize * scale;
            text.color = TextColor;

            if (font != null)
                text.font = font;

            var notice = view.AddComponent<RejectionNotice>();
            notice._group = view.GetComponent<CanvasGroup>();
            notice._group.alpha = 0f;
            notice._group.blocksRaycasts = false;
            notice._group.interactable = false;
            notice._text = text;
            view.SetActive(false);
            return notice;
        }

        public void Show(string message)
        {
            _text.text = message ?? string.Empty;
            _elapsed = 0f;
            _group.alpha = 0f;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            _elapsed = -1f;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_elapsed < 0f)
                return;

            _elapsed += Time.unscaledDeltaTime;

            float outStart = FadeInSeconds + HoldSeconds;

            if (_elapsed >= outStart + FadeOutSeconds)
            {
                Hide();
                return;
            }

            _group.alpha = _elapsed < FadeInSeconds
                ? _elapsed / FadeInSeconds
                : _elapsed < outStart ? 1f : 1f - (_elapsed - outStart) / FadeOutSeconds;
        }
    }
}
