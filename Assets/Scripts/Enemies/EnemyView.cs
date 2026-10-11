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
    // 달 성질이 붙은 적은 Breaker 달과 같은 모양의 달 하나가 주위를 공전한다. 크기·위상·속도는 출현 때 정해진다(일시정지 중에는 멈춘다).
    // 은하는 빛나는 중심 위에서 나선팔이 자전하고(공전과 별개, 화면에서만), 죽으면 나선이 중심으로 말려 들어가며 사라진다(GalaxyCollapses).
    // 픽업(혜성)은 CometView가 통째로 맡는다(스프라이트가 아니라 셰이더). 사망 파편·골드·피해량 텍스트도 없다 — 한 방에 부서지는 버프 아이템이라 필요 없다.
    // 목록에서 빠진 적(사망)의 스프라이트는 바로 지운다.
    // 피격·사망 연출(흔들림·파편·피해량·Gold 텍스트·파괴음)은 Step이 끝난 뒤 판의 피격 기록(World.Hits)과 사망 기록(World.Deaths)을 읽어 낸다.
    // 일시정지 중에는 판이 기록을 비우지 않으므로, 기록마다 번호(Sequence)로 걸러 한 번씩만 낸다.
    // 규칙 평면은 장면의 z = 0이고 x·y는 같다. HQ(원점)가 장면의 원점이다.
    internal sealed class EnemyView : IDisposable
    {
        // 적 하나의 화면 상태: 스프라이트와, 그 스프라이트에 적용되는 짧은 피격 흔들림 연출.
        private sealed class EnemyVisual
        {
            public SpriteRenderer Renderer;
            public EnemyHitAnimation Hit;
            // 달 성질이 붙은 적만 가진다(없으면 null). 적 스프라이트의 형제라 피격 흔들림·배율의 영향을 받지 않는다.
            public MeshRenderer Moon;
            // 달 공전의 위상(바퀴)과 속도(바퀴/초, 시계방향). 적마다 출현 때 한 번 정해진다.
            public float MoonPhase;
            public float MoonSpeed;
            // 은하만 가진다(없으면 null). 적 스프라이트(중심)의 자식이라 피격 흔들림과 크기를 함께 받고, 자기 자전만 따로 한다.
            public Transform Arms;
            // 나선팔 자전 속도(도/초, 음수 = 시계방향). 출현 때 한 번 정해진다.
            public float ArmSpin;
        }

        // 은하 나선팔의 자전 속도(도/초)와 적마다의 배율 범위 [임시]. 팔이 반시계로 감겨 있어 시계방향으로 돌면 팔이 뒤로 끌린다(EnemyLooks).
        private const float GalaxySpin = 40f;
        private const float GalaxySpinMin = 0.8f;
        private const float GalaxySpinMax = 1.2f;

        // 달 구체의 반지름(전장 배율 1에서의 월드 단위). 모든 행성이 같은 크기의 달을 가진다. Breaker 달(BreakerLook.OrbRadius)보다 작다.
        // 원작 달은 카메라와 무관하게 화면에서 약 25px이라(C0 지름 25px ÷ 54px/unit ÷ 2) 판의 전장 배율을 곱한다.
        private const float MoonRadius = 0.236f;
        // 공전 속도 배율의 범위. 기본 속도(BreakerLook.OrbitSpeed)에 곱해 적마다 다르게 돈다.
        private const float MoonSpeedMin = 0.8f;
        private const float MoonSpeedMax = 1.2f;
        // 달은 적 스프라이트(0)와 성질 속 채움(1) 위, Breaker 링 계열(5 ~ 11) 아래에 그린다.
        private const int MoonSortingOrder = 2;

        // 이 판의 전장 배율(Hq.FieldScale). 화면 크기가 고정인 달과 떠오르는 숫자에 곱한다. Synchronize가 매번 읽는다.
        private float _fieldScale = HqGrowthDefinition.StartFieldScale;
        private const float MeshMargin = 1.1f;
        // 셰이더(BlackHole/Breaker Orbs)의 MAX_ORBS와 같다. 달만 그리므로 칸 종류는 모두 0(달)이다.
        private const int MaxOrbs = 64;

        private static readonly int _orbitRadiusId = Shader.PropertyToID("_OrbitRadius");
        private static readonly int _orbRadiusId = Shader.PropertyToID("_OrbRadius");
        private static readonly int _orbCountId = Shader.PropertyToID("_OrbCount");
        private static readonly int _phaseId = Shader.PropertyToID("_Phase");
        private static readonly int _moonFillId = Shader.PropertyToID("_MoonFill");
        private static readonly int _moonOutlineId = Shader.PropertyToID("_MoonOutline");
        private static readonly int _outlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int _orbKindsId = Shader.PropertyToID("_OrbKinds");

        private readonly Transform _root;
        private readonly EnemyLooks _looks;
        private readonly BreakerLook _breakerLook;
        private readonly CometView _comets;
        private readonly Mesh _quad;
        private readonly MaterialPropertyBlock _properties = new MaterialPropertyBlock();
        private readonly float[] _moonKinds = new float[MaxOrbs];
        private readonly EnemyHitParticles _hitParticles;
        private readonly EnemyGoldText _goldText;
        private readonly EnemyDamageText _damageText;
        private readonly Dictionary<EnemyId, EnemyVisual> _visuals = new Dictionary<EnemyId, EnemyVisual>();
        private readonly HashSet<EnemyId> _seen = new HashSet<EnemyId>();
        private readonly List<EnemyId> _gone = new List<EnemyId>();
        // 이번 프레임에 사망 기록으로 죽음을 안 은하. 화면을 지우는 대신 붕괴 연출로 넘긴다.
        private readonly HashSet<EnemyId> _collapsing = new HashSet<EnemyId>();
        private readonly GalaxyCollapses _collapses = new GalaxyCollapses();

        // 마지막으로 연출을 낸 피격·사망 기록의 번호.
        private long _lastHitSequence;
        private long _lastDeathSequence;

        // breakerLook: 달의 외형(머티리얼·색·간격·기본 공전 속도)을 Breaker 달과 같게 맞추려고 받는다.
        // cometLook: 혜성(픽업)의 외형. 혜성의 화면은 CometView가 맡는다.
        public EnemyView(Transform parent, EnemyLooks looks, BreakerLook breakerLook, CometLook cometLook)
        {
            _root = new GameObject("Enemy View").transform;
            _root.SetParent(parent, false);
            _looks = looks;
            _breakerLook = breakerLook;
            _comets = new CometView(parent, cometLook);
            _quad = QuadRenderers.CreateMesh();
            _hitParticles = _root.gameObject.AddComponent<EnemyHitParticles>();
            _goldText = new EnemyGoldText(_root);
            _damageText = new EnemyDamageText(_root);
        }

        // paused: 일시정지 중이면 달 공전을 멈춘다.
        // delta: 이번 호출 사이 지난 시간. 피격 흔들림·달 공전 진행에 쓴다(즉시 스냅만 하고 싶을 때는 0을 넘긴다).
        public void Synchronize(World world, bool paused, float delta)
        {
            _seen.Clear();
            IReadOnlyList<Enemy> enemies = world.Enemies;
            _fieldScale = world.Hq.FieldScale;
            _goldText.Scale = _fieldScale;
            _damageText.Scale = _fieldScale;

            // 매 프레임 경로: IReadOnlyList를 인덱스로 돈다(인터페이스 foreach는 열거자를 할당한다).
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                _seen.Add(enemy.Id);

                if (enemy.Definition.IsPickup)
                {
                    _comets.Show(enemy, paused, delta);
                    continue;
                }

                if (!_visuals.TryGetValue(enemy.Id, out EnemyVisual visual))
                {
                    visual = Create(enemy);
                    _visuals.Add(enemy.Id, visual);
                }

                visual.Renderer.transform.localPosition = new Vector3(enemy.Position.X, enemy.Position.Y, 0);

                if (visual.Moon != null)
                    AdvanceMoon(visual, enemy, paused, delta);

                if (visual.Arms != null && !paused)
                    visual.Arms.Rotate(0, 0, visual.ArmSpin * delta);
            }

            // 이번 Step의 피격·사망 연출. 피격 흔들림은 아직 살아 있는 적에만 낸다(죽은 적의 스프라이트는 아래에서 지운다).
            ReadHits(world.Hits);
            ReadDeaths(world.Deaths);

            // 사망 파편의 흡입 스월, 떠오르는 텍스트 둘 다 개별 적이 아니라 공용 연출 하나를 매 프레임
            // 진행시키는 일이라, 위치 동기화와 같은 자리에서 한 번만 호출한다(따로 Update를 두지 않는다).
            _hitParticles.Advance();
            _goldText.Age(delta);
            _damageText.Age(delta);
            _collapses.Advance(paused, delta);
            _gone.Clear();

            foreach (EnemyId id in _visuals.Keys)
            {
                if (!_seen.Contains(id))
                    _gone.Add(id);
            }

            foreach (EnemyId id in _gone)
            {
                EnemyVisual visual = _visuals[id];

                if (visual.Arms != null && _collapsing.Contains(id))
                    _collapses.Play(visual.Renderer.transform, visual.Arms, visual.ArmSpin);
                else
                    Release(visual);

                _visuals.Remove(id);
            }

            _collapsing.Clear();

            _comets.Retain(_seen);
        }

        // 아직 읽지 않은 피격 기록마다: 살아 있는 적이면 흔들림, 그리고 피격 파편과 피해량 텍스트. 픽업(혜성)은 연출이 없다.
        private void ReadHits(IReadOnlyList<HitRecord> hits)
        {
            // 매 프레임 경로: IReadOnlyList를 인덱스로 돈다(인터페이스 foreach는 열거자를 할당한다).
            for (int i = 0; i < hits.Count; i++)
            {
                HitRecord hit = hits[i];

                if (hit.Sequence <= _lastHitSequence)
                    continue;

                _lastHitSequence = hit.Sequence;

                if (hit.IsPickup)
                    continue;

                if (_seen.Contains(hit.EnemyId) && _visuals.TryGetValue(hit.EnemyId, out EnemyVisual visual))
                    visual.Hit.Play();

                _hitParticles.EmitHit(new Vector3(hit.Position.X, hit.Position.Y, 0), _looks.ColorOf(hit.EnemyType, hit.Tier));
                _damageText.Show(hit);
            }
        }

        // 아직 읽지 않은 사망 기록마다 사망 파편과 Gold 텍스트(픽업 제외). 새 사망이 하나라도 있으면 파괴음을 한 번 낸다(겹쳐 커지지 않게).
        private void ReadDeaths(IReadOnlyList<DeathRecord> deaths)
        {
            bool anyNew = false;

            for (int i = 0; i < deaths.Count; i++)
            {
                DeathRecord death = deaths[i];

                if (death.Sequence <= _lastDeathSequence)
                    continue;

                _lastDeathSequence = death.Sequence;
                anyNew = true;

                if (death.IsPickup)
                    continue;

                _hitParticles.EmitDeath(new Vector3(death.Position.X, death.Position.Y, 0),
                    _looks.ColorOf(death.EnemyType, death.Tier), death.EnemyType, death.Tier, death.Size);
                _goldText.Show(death);

                if (death.EnemyType == EnemyType.Galaxy)
                    _collapsing.Add(death.EnemyId);
            }

            if (anyNew)
                SoundManager.Instance?.RequestDestroyed();
        }

        // 판이 바뀌거나 판을 정리할 때 모든 적 스프라이트를 지운다. 정리는 처치가 아니므로 연출도 없다.
        public void Reset()
        {
            foreach (EnemyVisual visual in _visuals.Values)
                Release(visual);

            _visuals.Clear();
            _collapsing.Clear();
            _collapses.Reset();
            _comets.Reset();
            _hitParticles.Clear();
            _goldText.Reset();
            _damageText.Reset();

            // 새 판의 기록 번호는 1부터 다시 시작하므로 함께 되돌린다. 빠뜨리면 다음 판에서 연출이 나지 않는다.
            _lastHitSequence = 0;
            _lastDeathSequence = 0;
        }

        public void Dispose()
        {
            _collapses.Reset();
            _comets.Dispose();
            Object.Destroy(_root.gameObject);
            Object.Destroy(_quad);
        }

        // 스프라이트·달을 지운다. _visuals에서 빼는 것은 호출자가 한다.
        // 혜성은 여기에 없다 — 화면은 CometView가 지운다(Retain·Reset).
        private void Release(EnemyVisual visual)
        {
            Object.Destroy(visual.Renderer.gameObject);

            if (visual.Moon != null)
                Object.Destroy(visual.Moon.gameObject);
        }

        private EnemyVisual Create(Enemy enemy)
        {
            EnemyType kind = enemy.Definition.Type;
            EnemyTraitType? trait = enemy.Trait?.Type;
            var view = new GameObject(trait != null ? $"{kind} #{enemy.Id.Value} ({trait})" : $"{kind} #{enemy.Id.Value}");
            view.transform.SetParent(_root, false);

            var renderer = view.AddComponent<SpriteRenderer>();
            // 은하는 빛나는 중심을 적 스프라이트로 쓰고, 나선팔은 아래 자식으로 따로 그린다(CreateGalaxyArms).
            renderer.sprite = kind == EnemyType.Galaxy ? _looks.GalaxyCore : _looks.SpriteOf(kind, enemy.Id.Value);
            renderer.color = _looks.ColorOf(kind, enemy.Tier);

            // 크기는 규칙 수치(반지름)를 그대로 쓴다: 스프라이트의 긴 변이 지름이 되게 맞춘다.
            Vector3 bounds = renderer.sprite.bounds.size;
            float longest = Mathf.Max(bounds.x, bounds.y);
            view.transform.localScale = Vector3.one * (enemy.Stats.Radius * 2 / longest);

            Color marker = _looks.TraitColorOf(kind, trait);

            if (marker.a > 0)
            {
                var fill = new GameObject(trait.ToString());
                fill.transform.SetParent(view.transform, false);
                fill.transform.localScale = Vector3.one * EnemyLooks.TraitFillScale;

                var fillRenderer = fill.AddComponent<SpriteRenderer>();
                fillRenderer.sprite = renderer.sprite;
                fillRenderer.color = marker;
                fillRenderer.sortingOrder = renderer.sortingOrder + 1;
            }

            var visual = new EnemyVisual { Renderer = renderer, Hit = new EnemyHitAnimation(renderer.transform) };

            if (trait == EnemyTraitType.Moon)
                CreateMoon(visual, enemy);

            if (kind == EnemyType.Galaxy)
                CreateGalaxyArms(visual, enemy);

            return visual;
        }

        // 은하의 나선팔: 중심 스프라이트의 자식으로, 중심과 같은 색·크기 틀로 그린다. 중심(0)보다 한 칸 아래에 그려 중심 빛이 팔 위에 겹친다.
        // 피격 흔들림은 부모(중심)를 돌리므로 팔도 함께 흔들리고, 자전은 팔만 돈다.
        private void CreateGalaxyArms(EnemyVisual visual, Enemy enemy)
        {
            var arms = new GameObject("Arms");
            arms.transform.SetParent(visual.Renderer.transform, false);
            arms.transform.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(0f, 360f));

            var armsRenderer = arms.AddComponent<SpriteRenderer>();
            armsRenderer.sprite = _looks.SpriteOf(EnemyType.Galaxy, enemy.Id.Value);
            armsRenderer.color = visual.Renderer.color;
            armsRenderer.sortingOrder = visual.Renderer.sortingOrder - 1;

            // 에셋 스프라이트로 바꿔도 중심과 같은 지름이 되게 맞춘다(중심 도형은 1 단위).
            Vector3 bounds = armsRenderer.sprite.bounds.size;
            float longest = Mathf.Max(bounds.x, bounds.y);
            Vector3 core = visual.Renderer.sprite.bounds.size;
            arms.transform.localScale = Vector3.one * (Mathf.Max(core.x, core.y) / longest);

            visual.Arms = arms.transform;
            visual.ArmSpin = -GalaxySpin * UnityEngine.Random.Range(GalaxySpinMin, GalaxySpinMax);
        }

        // 달은 출현 때 한 번 만든다. 적의 크기는 변하지 않으므로 궤도와 사각형 크기도 여기서 한 번만 정한다.
        // 외형은 Breaker 달과 같은 셰이더·머티리얼이다. 궤도는 적의 반지름 바로 바깥이다(Breaker는 링 바깥 가장자리).
        private void CreateMoon(EnemyVisual visual, Enemy enemy)
        {
            var moon = QuadRenderers.Create($"Moon #{enemy.Id.Value}", _root, _quad, _breakerLook.OrbMaterial, MoonSortingOrder);
            // 적(월드)에 붙어 돌지만 달 자체와 간격은 화면 크기가 고정이다: 전장 배율을 곱한다.
            float moonRadius = MoonRadius * _fieldScale;
            float orbitRadius = enemy.Stats.Radius + _breakerLook.OrbitOffset * _fieldScale + moonRadius;
            float size = 2 * (orbitRadius + moonRadius) * MeshMargin;
            moon.transform.localScale = new Vector3(size, size, 1);

            moon.GetPropertyBlock(_properties);
            _properties.SetFloat(_orbitRadiusId, orbitRadius);
            _properties.SetFloat(_orbRadiusId, moonRadius);
            _properties.SetFloat(_orbCountId, 1);
            _properties.SetColor(_moonFillId, _breakerLook.MoonFill);
            _properties.SetColor(_moonOutlineId, _breakerLook.MoonOutline);
            _properties.SetFloat(_outlineWidthId, _breakerLook.MoonOutlineWidth * _fieldScale);
            _properties.SetFloatArray(_orbKindsId, _moonKinds);
            moon.SetPropertyBlock(_properties);

            moon.enabled = true;
            visual.Moon = moon;
            visual.MoonPhase = UnityEngine.Random.value;
            visual.MoonSpeed = _breakerLook.OrbitSpeed * UnityEngine.Random.Range(MoonSpeedMin, MoonSpeedMax);
            moon.transform.localPosition = new Vector3(enemy.Position.X, enemy.Position.Y, 0);
            SetMoonPhase(visual);
        }

        private void AdvanceMoon(EnemyVisual visual, Enemy enemy, bool paused, float delta)
        {
            visual.Moon.transform.localPosition = new Vector3(enemy.Position.X, enemy.Position.Y, 0);

            if (paused || delta <= 0)
                return;

            // 시계방향으로 돌므로 위상이 줄어든다.
            visual.MoonPhase = Mathf.Repeat(visual.MoonPhase - visual.MoonSpeed * delta, 1);
            SetMoonPhase(visual);
        }

        private void SetMoonPhase(EnemyVisual visual)
        {
            visual.Moon.GetPropertyBlock(_properties);
            _properties.SetFloat(_phaseId, visual.MoonPhase);
            visual.Moon.SetPropertyBlock(_properties);
        }
    }
}
