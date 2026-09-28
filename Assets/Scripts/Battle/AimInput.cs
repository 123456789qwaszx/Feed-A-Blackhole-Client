using BlackHole.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BlackHole.Unity
{
    // 조준 입력: 마우스 포인터의 화면 위치를 규칙 평면의 점으로 바꿔, 진행 중인 판의 로컬 참가자 조준점에 넣는다(SYSTEM_CATALOG S02).
    // 마우스는 지금의 입력 출처일 뿐이다 — 판은 조준점만 안다. 규칙 평면은 장면의 z = 0이고 HQ가 장면의 원점이다.
    // 시작하거나 정리할 상태가 없다. 진행 중인 판이 없으면 아무것도 하지 않는다.
    internal sealed class AimInput
    {
        private readonly BattleSystem _battle;
        private readonly PlayerId _player;

        public AimInput(BattleSystem battle, PlayerId player)
        {
            _battle = battle;
            _player = player;
        }

        // 전투 시스템보다 먼저 부른다. 이번 프레임의 Step이 이 조준점으로 공격한다.
        public void Tick()
        {
            if (!_battle.IsRunning)
                return;

            _battle.Session.SetAimPoint(_player, Read());
        }

        // 마우스나 카메라가 없으면 조준점도 없다.
        private static Point2? Read()
        {
            Camera camera = Camera.main;
            Mouse mouse = Mouse.current;

            if (camera == null || mouse == null)
                return null;

            Vector2 screen = mouse.position.ReadValue();
            Vector3 point = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -camera.transform.position.z));
            return new Point2(point.x, point.y);
        }
    }
}
