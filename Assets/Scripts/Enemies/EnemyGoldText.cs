using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 적 사망으로 얻은 Gold를 그 자리 위로 "+$10" 형태로 띄우는 연출.
    // 황금(GoldenDefinition) 성질이 붙어 Gold를 크게 받은 사망은 고정된 금색으로 띄운다.
    internal sealed class EnemyGoldText : EnemyFloatingText
    {
        private const int MaxLabels = 16;
        private const float RiseDistance = 0.4f;
        private const float Duration = 1.6f;
        private const int SortingOrder = 20;
        // TMP 폰트 크기는 폰트 에셋 메트릭에 따라 달라진다. 적 반지름(0.2~0.3 안팎)에 맞춰 에디터에서 눈으로 보고 조절할 값이다.
        private const float FontSize = 4f;
        private static readonly Color NormalColor = new Color32(0x32, 0xA4, 0x62, 0xFF);
        private static readonly Color GoldenColor = new Color32(0xCC, 0xB0, 0x50, 0xFF);

        public EnemyGoldText(Transform parent)
            : base(parent, "Gold Text", MaxLabels, RiseDistance, Duration, SortingOrder, FontSize)
        {
        }

        public void Register(Enemy enemy) => enemy.Died += OnDied;
        public void Unregister(Enemy enemy) => enemy.Died -= OnDied;

        private void OnDied(Enemy enemy)
        {
            if (enemy.Stats.Gold <= 0)
                return;

            // 황금 성질로 Gold를 더 받은 사망만 고정된 금색으로 띄운다.
            bool isGolden = enemy.Trait?.Effect is GoldenDefinition;
            Color color = isGolden ? GoldenColor : NormalColor;

            Show(new Vector3(enemy.Position.X, enemy.Position.Y, 0), $"+${enemy.Stats.Gold}", color);
        }
    }
}
