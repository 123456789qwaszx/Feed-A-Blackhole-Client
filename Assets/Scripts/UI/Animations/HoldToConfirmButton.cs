using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    // 꾹 눌러 확정하는 버튼: 누르고 HoldSeconds가 찰 때까지 안에 머문 뒤, 버튼 안쪽에서 떼면 Confirmed.
    // - 차오르는 동안 Fill(가로로 채우는 Image)이 0 → 1로 찬다. 밖으로 나가면 바로 비우고, 밖에서 떼면 확정하지 않는다.
    // - 톡 치면(시간이 차기 전에 떼면) 아무 일도 없다 — 읽으려고 누른 손가락이 사지 않게.
    // - 끌기 핸들러를 두지 않는다. UI_EventHandler의 롱프레스는 EventSystem.pixelDragThreshold만 넘어도 끊겨 꾹 누르는 손가락이 흔들리면 실패하므로 쓰지 않는다.
    // - 처음 누른 포인터 하나만 따른다. 두 번째 손가락은 무시한다. 비활성(Interactable = false)이면 누름도 받지 않는다.
    public sealed class HoldToConfirmButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private const int NoPointer = int.MinValue;

        [Tooltip("확정까지 눌러야 하는 시간(초).")]
        [SerializeField, Min(0.05f)] private float _holdSeconds = 0.25f;
        [Tooltip("차오름을 보이는 그림(Image Type = Filled). 비워도 된다.")]
        [SerializeField] private Image _fill;

        public event Action Confirmed;

        public float HoldSeconds
        {
            get => _holdSeconds;
            set => _holdSeconds = Mathf.Max(0.05f, value);
        }

        public Image Fill
        {
            get => _fill;
            set => _fill = value;
        }

        public bool Interactable { get; set; } = true;

        // 지금 꾹 누르는 중인가(차오르는 중).
        public bool IsHolding => _pointerId != NoPointer && _inside;

        private int _pointerId = NoPointer;
        private bool _inside;
        private float _held;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!Interactable || _pointerId != NoPointer)
                return;

            _pointerId = eventData.pointerId;
            _inside = true;
            _held = 0;
            SetFill(0);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (eventData.pointerId == _pointerId)
                _inside = true;
        }

        // 누른 채 밖으로 나가면 처음부터 다시 — 돌아와도 0에서 찬다.
        public void OnPointerExit(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId)
                return;

            _inside = false;
            _held = 0;
            SetFill(0);
        }

        // EventSystem은 밖에서 떼도 누른 대상에 PointerUp을 준다. 안쪽이고 시간이 찼을 때만 확정한다.
        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId)
                return;

            bool confirm = Interactable && _inside && _held >= _holdSeconds;
            Cancel();

            if (confirm)
                Confirmed?.Invoke();
        }

        private void Update()
        {
            if (!IsHolding)
                return;

            // 일시정지(timeScale 0)와 무관하게 실제 시간으로 찬다.
            _held += Time.unscaledDeltaTime;
            SetFill(Mathf.Clamp01(_held / _holdSeconds));
        }

        // 누름을 버린다(확정 없이). 비활성·숨김·내용이 바뀔 때.
        public void Cancel()
        {
            _pointerId = NoPointer;
            _inside = false;
            _held = 0;
            SetFill(0);
        }

        private void OnDisable() => Cancel();

        private void SetFill(float amount)
        {
            if (_fill != null)
                _fill.fillAmount = amount;
        }
    }
}
