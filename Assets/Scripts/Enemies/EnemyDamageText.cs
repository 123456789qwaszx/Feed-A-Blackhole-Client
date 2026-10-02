using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 적이 맞은 피해량을 그 자리 위로 띄우는 연출. 일반 피해는 검정, 치명타는 붉은색이다.
    internal sealed class EnemyDamageText : EnemyFloatingText
    {
        private const int MaxLabels = 16;
        private const float RiseDistance = -0.4f;
        private const float Duration = 1.6f;
        private const int SortingOrder = 21;
        private const float FontSize = 3f;

        // 지금은 기본(밝은) 배경 기준 검정으로 고정. 다크모드가 생기면 이 값을 그때그때 맞는 색으로 바꿔 주면 된다
        // (예: 설정이 바뀔 때 GameSettings.IsOn(GameSettings.DarkMode) 결과에 따라 흰색/검정으로 바꿔서 대입).
        // const가 아니라 static 필드인 이유: 나중에 바깥에서 값을 바꿔 끼울 수 있어야 하기 때문이다.
        public static Color NormalColor = Color.black;
        private static readonly Color CriticalColor = new Color32(0xFF, 0x3B, 0x30, 0xFF);

        public EnemyDamageText(Transform parent)
            : base(parent, "Damage Text", MaxLabels, RiseDistance, Duration, SortingOrder, FontSize)
        {
        }

        public void Register(Enemy enemy) => enemy.Damaged += OnDamaged;
        public void Unregister(Enemy enemy) => enemy.Damaged -= OnDamaged;

        private void OnDamaged(Enemy enemy, Damage damage)
        {
            Color color = damage.IsCritical ? CriticalColor : NormalColor;
            Show(new Vector3(enemy.Position.X, enemy.Position.Y, 0), Mathf.RoundToInt(damage.Amount).ToString(), color);
        }
    }
}
