using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlackHole.Unity
{
    // 적 시스템의 화면. 매 프레임 World의 살아 있는 적을 읽어 스프라이트를 맞춘다. 게임 상태를 바꾸지 않는다.
    // 외형은 적 종류 에셋이 가진다(EnemyLooks). 색은 적의 색 등급으로, 크기는 적의 수치로 정한다.
    // 황금이면 그 색의 윤곽 안에 노란 속을 한 겹 더 그린다. 스프라이트가 없는 종류는 임시 원으로 그린다.
    // 목록에서 빠진 적(사망)의 스프라이트는 바로 지운다. 파괴·흡수 연출은 연출 작업에서 사망 기록을 읽어 더한다.
    // 규칙 평면은 장면의 z = 0이고 x·y는 같다. HQ(원점)가 장면의 원점이다.
    internal sealed class EnemyView : IDisposable
    {
        private readonly Transform _root;
        private readonly EnemyLooks _looks;
        private readonly Dictionary<EnemyId, SpriteRenderer> _views = new Dictionary<EnemyId, SpriteRenderer>();
        private readonly HashSet<EnemyId> _seen = new HashSet<EnemyId>();
        private readonly List<EnemyId> _gone = new List<EnemyId>();

        public EnemyView(Transform parent, EnemyLooks looks)
        {
            _root = new GameObject("Enemy View").transform;
            _root.SetParent(parent, false);
            _looks = looks;
        }

        public void Synchronize(World world)
        {
            _seen.Clear();
            IReadOnlyList<Enemy> enemies = world.Enemies;

            // 매 프레임 경로: IReadOnlyList를 인덱스로 돈다(인터페이스 foreach는 열거자를 할당한다).
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                _seen.Add(enemy.Id);

                if (!_views.TryGetValue(enemy.Id, out SpriteRenderer view))
                {
                    view = Create(enemy);
                    _views.Add(enemy.Id, view);
                }

                view.transform.localPosition = new Vector3(enemy.Position.X, enemy.Position.Y, 0);
            }

            _gone.Clear();

            foreach (EnemyId id in _views.Keys)
            {
                if (!_seen.Contains(id))
                    _gone.Add(id);
            }

            foreach (EnemyId id in _gone)
            {
                Object.Destroy(_views[id].gameObject);
                _views.Remove(id);
            }
        }

        // 관리하는 적 스프라이트가 없고, 지운 객체도 장면에서 모두 사라졌는가.
        // 지운 객체는 프레임 끝에 사라지므로, Reset 뒤 한 프레임이 지나야 true가 된다.
        public bool IsClear => _views.Count == 0 && _root.childCount == 0;

        // 판이 바뀌거나 판을 정리할 때 모든 적 스프라이트를 지운다. 정리는 처치가 아니므로 연출도 없다.
        public void Reset()
        {
            foreach (SpriteRenderer view in _views.Values)
                Object.Destroy(view.gameObject);

            _views.Clear();
        }

        public void Dispose() => Object.Destroy(_root.gameObject);

        private SpriteRenderer Create(Enemy enemy)
        {
            string kind = enemy.Definition.Id;
            var view = new GameObject(enemy.IsGolden ? $"{kind} #{enemy.Id.Value} (golden)" : $"{kind} #{enemy.Id.Value}");
            view.transform.SetParent(_root, false);

            var renderer = view.AddComponent<SpriteRenderer>();
            renderer.sprite = _looks.SpriteOf(kind);
            renderer.color = _looks.ColorOf(kind, enemy.Tier);

            // 크기는 규칙 수치(반지름)를 그대로 쓴다: 스프라이트의 긴 변이 지름이 되게 맞춘다.
            Vector3 bounds = renderer.sprite.bounds.size;
            float longest = Mathf.Max(bounds.x, bounds.y);
            view.transform.localScale = Vector3.one * (enemy.Stats.Size * 2 / longest);

            if (enemy.IsGolden)
            {
                var fill = new GameObject("Golden");
                fill.transform.SetParent(view.transform, false);
                fill.transform.localScale = Vector3.one * EnemyLooks.GoldenFillScale;

                var fillRenderer = fill.AddComponent<SpriteRenderer>();
                fillRenderer.sprite = renderer.sprite;
                fillRenderer.color = EnemyLooks.GoldenFill;
                fillRenderer.sortingOrder = renderer.sortingOrder + 1;
            }

            return renderer;
        }
    }
}
