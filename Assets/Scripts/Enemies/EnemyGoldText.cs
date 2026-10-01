using System.Collections.Generic;
using BlackHole.Core;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlackHole.Unity
{
    // 적 사망으로 얻은 Gold를 그 자리 위로 "+$10" 형태로 띄우는 연출.
    // EnemyView.Synchronize가 매 프레임 Age를 불러 구동한다(개별 Update 없음, ExplosionRings와 같은 방식).
    // 라벨은 재사용한다(쉬는 라벨을 먼저 쓰고, 상한이면 가장 오래 재생된 라벨을 다시 쓴다). 규칙 평면은 장면의 z = 0이다.
    // 황금(GoldenDefinition) 성질이 붙어 Gold를 크게 받은 사망은, 그 적이 화면에서 쓰는 표식 색(EnemyLooks.TraitColorOf)과 같은 색으로 띄운다.
    internal sealed class EnemyGoldText
    {
        private const int MaxLabels = 16;
        private const float RiseDistance = 0.4f;
        private const float Duration = 1.6f;
        private const int SortingOrder = 20;
        // TMP 폰트 크기는 폰트 에셋 메트릭에 따라 달라진다. 적 반지름(0.2~0.3 안팎)에 맞춰 에디터에서 눈으로 보고 조절할 값이다.
        private const float FontSize = 4f;
        private static readonly Color NormalColor = new Color32(0x32, 0xA4, 0x62, 0xFF);

        private sealed class Label
        {
            public TextMeshPro Text;
            public Vector3 Start;
            public float Elapsed;
            public bool Playing;
        }

        private readonly Transform _root;
        private readonly EnemyLooks _looks;
        private readonly List<Label> _labels = new List<Label>();

        public EnemyGoldText(Transform parent, EnemyLooks looks)
        {
            _root = new GameObject("Gold Text").transform;
            _root.SetParent(parent, false);
            _looks = looks;
        }

        public void Register(Enemy enemy) => enemy.Died += OnDied;
        public void Unregister(Enemy enemy) => enemy.Died -= OnDied;

        // 떠 있는 라벨이 없고, 지운 객체도 장면에서 모두 사라졌는가.
        // 지운 객체는 프레임 끝에 사라지므로, Reset 뒤 한 프레임이 지나야 true가 된다.
        public bool IsClear => _labels.Count == 0 && _root.childCount == 0;

        private void OnDied(Enemy enemy)
        {
            if (enemy.Stats.Gold <= 0)
                return;

            // 황금 성질로 Gold를 더 받은 사망만, 그 적이 쓰는 표식 색(금색)으로 띄운다.
            bool isGolden = enemy.Trait?.Effect is GoldenDefinition;
            Color color = isGolden ? _looks.TraitColorOf(enemy.Definition.Id, enemy.Trait.Id) : NormalColor;

            Label label = Take();
            label.Start = new Vector3(enemy.Position.X, enemy.Position.Y, 0);
            label.Elapsed = 0;
            label.Playing = true;
            label.Text.text = $"+${enemy.Stats.Gold}";
            label.Text.color = color;
            label.Text.transform.position = label.Start;
            label.Text.gameObject.SetActive(true);
        }

        // 떠 있는 라벨을 위로 올리며 옅어지게 하고, 다 끝나면 숨긴다(다음 Take까지 재사용 대기).
        public void Age(float delta)
        {
            for (int i = 0; i < _labels.Count; i++)
            {
                Label label = _labels[i];

                if (!label.Playing)
                    continue;

                label.Elapsed += delta;

                if (label.Elapsed >= Duration)
                {
                    label.Playing = false;
                    label.Text.gameObject.SetActive(false);
                    continue;
                }

                float progress = label.Elapsed / Duration;
                label.Text.transform.position = label.Start + Vector3.up * (RiseDistance * progress);

                Color color = label.Text.color;
                color.a = 1f - progress;
                label.Text.color = color;
            }
        }

        // 판이 바뀌거나 판을 정리할 때 모든 라벨을 지운다.
        public void Reset()
        {
            foreach (Label label in _labels)
                Object.Destroy(label.Text.gameObject);

            _labels.Clear();
        }

        // 쉬는 라벨을 먼저 쓴다. 없으면 상한까지 새로 만들고, 상한이면 가장 오래 재생된 라벨을 다시 쓴다.
        private Label Take()
        {
            for (int i = 0; i < _labels.Count; i++)
            {
                if (!_labels[i].Playing)
                    return _labels[i];
            }

            if (_labels.Count < MaxLabels)
            {
                Label created = Create();
                _labels.Add(created);
                return created;
            }

            Label oldest = _labels[0];

            for (int i = 1; i < _labels.Count; i++)
            {
                if (_labels[i].Elapsed > oldest.Elapsed)
                    oldest = _labels[i];
            }

            return oldest;
        }

        private Label Create()
        {
            var view = new GameObject("Gold Text Label");
            view.transform.SetParent(_root, false);
            view.SetActive(false);

            var text = view.AddComponent<TextMeshPro>();
            text.fontSize = FontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = NormalColor;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.GetComponent<MeshRenderer>().sortingOrder = SortingOrder;

            return new Label { Text = text };
        }
    }
}
