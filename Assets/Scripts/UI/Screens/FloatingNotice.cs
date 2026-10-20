using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    // 화면 위쪽에 잠깐 뜨는 알림 글자(서버 연결 결과, 동기화 거부; 계약 2.8, 5.9). 프리팹 없이 코드로 만든다.
    // 나타남 → 머묾 → 사라짐 순서로 혼자 끝나고, 입력을 가로채지 않는다. 실제 시간으로 흐른다(timeScale과 무관).
    // 알림은 하나씩만 보인다. 보이는 중에 Enqueue하면 줄을 서서 앞의 알림이 사라진 뒤 차례로 보인다(글자가 겹치지 않는다).
    // 글자는 호출하는 쪽이 번역해서 넘긴다.
    [DisallowMultipleComponent]
    internal sealed class FloatingNotice : MonoBehaviour
    {
        private const float FadeInSeconds = 0.3f;
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

        private readonly Queue<(string Text, float Hold)> _pending = new();
        private CanvasGroup _group;
        private TMP_Text _text;
        private float _hold;
        private float _elapsed = -1f;

        // 지금 보이는 알림이 있거나 줄 서 있는 알림이 있는가.
        public bool IsBusy => _elapsed >= 0f || _pending.Count > 0;

        public static FloatingNotice Create(Transform canvas, int siblingIndex, TMP_FontAsset font, float scale)
        {
            if (canvas == null)
                throw new ArgumentNullException(nameof(canvas));

            scale = scale > 0 ? scale : 1;

            var view = new GameObject("Floating Notice", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
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

            var notice = view.AddComponent<FloatingNotice>();
            notice._group = view.GetComponent<CanvasGroup>();
            notice._group.alpha = 0f;
            notice._group.blocksRaycasts = false;
            notice._group.interactable = false;
            notice._text = text;
            view.SetActive(false);
            return notice;
        }

        // 알림을 줄 세운다. 보이는 알림이 없으면 바로 보인다. holdSeconds는 다 나타난 뒤 머무는 시간이다.
        public void Enqueue(string message, float holdSeconds)
        {
            _pending.Enqueue((message ?? string.Empty, Mathf.Max(0f, holdSeconds)));

            if (_elapsed < 0f)
                ShowNext();
        }

        // 보이는 알림과 줄 서 있는 알림을 모두 지운다.
        public void Clear()
        {
            _pending.Clear();
            _elapsed = -1f;
            gameObject.SetActive(false);
        }

        private void ShowNext()
        {
            if (_pending.Count == 0)
            {
                _elapsed = -1f;
                gameObject.SetActive(false);
                return;
            }

            (string text, float hold) = _pending.Dequeue();
            _text.text = text;
            _hold = hold;
            _elapsed = 0f;
            _group.alpha = 0f;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (_elapsed < 0f)
                return;

            _elapsed += Time.unscaledDeltaTime;

            float outStart = FadeInSeconds + _hold;

            if (_elapsed >= outStart + FadeOutSeconds)
            {
                ShowNext();
                return;
            }

            _group.alpha = _elapsed < FadeInSeconds
                ? _elapsed / FadeInSeconds
                : _elapsed < outStart ? 1f : 1f - (_elapsed - outStart) / FadeOutSeconds;
        }
    }
}
