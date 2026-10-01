using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlackHole.Unity
{
    // 적 시스템의 화면. 매 프레임 World의 살아 있는 적을 읽어 스프라이트를 맞추고, 짧은 피격 흔들림 연출도 같이 진행시킨다.
    // 게임 상태를 바꾸지 않는다. 외형은 적 종류 에셋이 가진다(EnemyLooks). 색은 적의 색 등급으로, 크기는 적의 수치로 정한다.   
    // 특수 성질이 붙었으면 그 색의 윤곽 안에 성질의 표식 색으로 속을 한 겹 더 그린다(황금이면 노란 속, 임시 표식). 스프라이트가 없는 종류는 적 ID에 맞는 다각형으로 그린다.
    // 목록에서 빠진 적(사망)의 스프라이트는 바로 지운다. 파괴·흡수 연출은 연출 작업에서 사망 기록을 읽어 더한다.
    // 규칙 평면은 장면의 z = 0이고 x·y는 같다. HQ(원점)가 장면의 원점이다.
    internal sealed class EnemyView : IDisposable
    {
        // 적 하나의 화면 상태: 스프라이트와, 그 스프라이트에 적용되는 짧은 피격 흔들림 연출.
        // 둘을 한 쌍으로 묶어 두면 EnemyId 하나당 여러 딕셔너리를 오가며 맞출 필요가 없다.
        private sealed class EnemyVisual
        {
            public SpriteRenderer Renderer;
            public EnemyHitAnimation Hit;
        }

        private readonly Transform _root;
        private readonly EnemyLooks _looks;
        private readonly EnemyHitParticles _hitParticles;
        private readonly Dictionary<EnemyId, EnemyVisual> _visuals = new Dictionary<EnemyId, EnemyVisual>();
        private readonly Dictionary<EnemyId, Enemy> _models = new Dictionary<EnemyId, Enemy>();
        private readonly HashSet<EnemyId> _seen = new HashSet<EnemyId>();
        private readonly List<EnemyId> _gone = new List<EnemyId>();

        public EnemyView(Transform parent, EnemyLooks looks)
        {
            _root = new GameObject("Enemy View").transform;
            _root.SetParent(parent, false);
            _looks = looks;
            _hitParticles = _root.gameObject.AddComponent<EnemyHitParticles>();
        }

        // delta: 이번 호출 사이 지난 시간. 피격 흔들림 진행에 쓴다(즉시 스냅만 하고 싶을 때는 0을 넘긴다).
        public void Synchronize(World world, float delta)
        {
            _seen.Clear();
            IReadOnlyList<Enemy> enemies = world.Enemies;

            // 매 프레임 경로: IReadOnlyList를 인덱스로 돈다(인터페이스 foreach는 열거자를 할당한다).
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                _seen.Add(enemy.Id);

                if (!_visuals.TryGetValue(enemy.Id, out EnemyVisual visual))
                {
                    visual = Create(enemy);
                    _visuals.Add(enemy.Id, visual);
                    _models.Add(enemy.Id, enemy);
                    enemy.Damaged += visual.Hit.Play;
                    _hitParticles.Register(enemy, _looks.ColorOf(enemy.Definition.Id, enemy.Tier));
                }

                visual.Renderer.transform.localPosition = new Vector3(enemy.Position.X, enemy.Position.Y, 0);
                visual.Hit.Advance(delta, visual.Renderer.transform);
            }

            // 사망 파편의 흡입 스월은 개별 적이 아니라 공용 파티클 시스템 하나를 매 프레임 진행시키는 일이라,
            // 위치/피격 흔들림과 같은 자리에서 한 번만 호출
            _hitParticles.Advance();
            _gone.Clear();

            foreach (EnemyId id in _visuals.Keys)
            {
                if (!_seen.Contains(id))
                    _gone.Add(id);
            }

            foreach (EnemyId id in _gone)
            {
                _models[id].Damaged -= _visuals[id].Hit.Play;
                _hitParticles.Unregister(_models[id]);
                Object.Destroy(_visuals[id].Renderer.gameObject);
                _visuals.Remove(id);
                _models.Remove(id);
            }
        }

        // 관리하는 적 스프라이트가 없고, 지운 객체도 장면에서 모두 사라졌는가.
        // 지운 객체는 프레임 끝에 사라지므로, Reset 뒤 한 프레임이 지나야 true가 된다.
        public bool IsClear => _visuals.Count == 0 && _root.childCount == 1 && _hitParticles.IsClear;

        // 판이 바뀌거나 판을 정리할 때 모든 적 스프라이트를 지운다. 정리는 처치가 아니므로 연출도 없다.
        public void Reset()
        {
            foreach (KeyValuePair<EnemyId, EnemyVisual> entry in _visuals)
            {
                _models[entry.Key].Damaged -= entry.Value.Hit.Play;
                _hitParticles.Unregister(_models[entry.Key]);
                Object.Destroy(entry.Value.Renderer.gameObject);
            }

            _visuals.Clear();
            _models.Clear();
            _hitParticles.Clear();
        }

        public void Dispose() => Object.Destroy(_root.gameObject);

        private EnemyVisual Create(Enemy enemy)
        {
            string kind = enemy.Definition.Id;
            string trait = enemy.Trait?.Id;
            var view = new GameObject(trait != null ? $"{kind} #{enemy.Id.Value} ({trait})" : $"{kind} #{enemy.Id.Value}");
            view.transform.SetParent(_root, false);

            var renderer = view.AddComponent<SpriteRenderer>();
            renderer.sprite = _looks.SpriteOf(kind, enemy.Id.Value);
            renderer.color = _looks.ColorOf(kind, enemy.Tier);

            // 크기는 규칙 수치(반지름)를 그대로 쓴다: 스프라이트의 긴 변이 지름이 되게 맞춘다.
            Vector3 bounds = renderer.sprite.bounds.size;
            float longest = Mathf.Max(bounds.x, bounds.y);
            view.transform.localScale = Vector3.one * (enemy.Stats.Size * 2 / longest);

            Color marker = _looks.TraitColorOf(kind, trait);

            if (marker.a > 0)
            {
                var fill = new GameObject(trait);
                fill.transform.SetParent(view.transform, false);
                fill.transform.localScale = Vector3.one * EnemyLooks.TraitFillScale;

                var fillRenderer = fill.AddComponent<SpriteRenderer>();
                fillRenderer.sprite = renderer.sprite;
                fillRenderer.color = marker;
                fillRenderer.sortingOrder = renderer.sortingOrder + 1;
            }

            return new EnemyVisual { Renderer = renderer, Hit = new EnemyHitAnimation() };
        }
    }
}
