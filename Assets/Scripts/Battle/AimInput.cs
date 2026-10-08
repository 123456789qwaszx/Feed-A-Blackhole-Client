using BlackHole.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace BlackHole.Unity
{
    // 조준 입력: 포인터(마우스·펜·터치 중 마지막으로 쓴 것)의 화면 위치를 규칙 평면의 점으로 바꿔, 진행 중인 판의 조준점(Breaker가 치는 자리)에 넣는다(SYSTEM_CATALOG S02).
    // - 마우스·펜: 포인터가 있는 곳을 늘 조준한다.
    // - 터치: 손가락이 닿아 있는 동안만 조준한다. 떼면 조준점이 없다. UI(버튼 등) 위를 누른 터치는 조준하지 않는다.
    //   Device Simulator도 마우스 조작을 터치로 넘기므로 이 경로를 탄다.
    // 포인터는 지금의 입력 출처일 뿐이다 — 판은 조준점만 안다. 규칙 평면은 장면의 z = 0이고 HQ가 장면의 원점이다.
    // 시작하거나 정리할 상태가 없다. 진행 중인 판이 없으면 아무것도 하지 않는다.
    internal sealed class AimInput
    {
        private readonly BattleSystem _battle;

        public AimInput(BattleSystem battle) => _battle = battle;

        // 전투 시스템보다 먼저 부른다. 이번 프레임의 Step이 이 조준점으로 공격한다.
        public void Tick()
        {
            if (!_battle.IsRunning)
                return;

            _battle.Session.SetAimPoint(Read());
        }

        // 포인터나 카메라가 없으면 조준점도 없다.
        private static Point2? Read()
        {
            Camera camera = Camera.main;
            Pointer pointer = Pointer.current;

            if (camera == null || pointer == null)
                return null;

            if (pointer is Touchscreen touchscreen && !IsAimingTouch(touchscreen.primaryTouch))
                return null;

            Vector2 screen = pointer.position.ReadValue();
            Vector3 point = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -camera.transform.position.z));
            return new Point2(point.x, point.y);
        }

        // 손가락이 닿아 있고, UI 위를 누른 것이 아닌 터치.
        private static bool IsAimingTouch(TouchControl touch)
        {
            if (!touch.press.isPressed)
                return false;

            EventSystem events = EventSystem.current;
            return events == null || !events.IsPointerOverGameObject(touch.touchId.ReadValue());
        }
    }
}
