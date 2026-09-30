using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlackHole.Unity
{
    // Breaker의 화면. 매 프레임 판의 참가자를 읽어 참가자마다 점선 링 하나를 조준점에 그린다.
    // 게임 상태를 바꾸지 않고, 피해를 다시 계산하지 않는다. 외형은 BreakerLook이 가진다.
    // - 링의 기본 반지름은 판정 반지름이다(GAME_RULES 6절). 달 버프로 판정 반지름이 바뀌면 매 프레임 따라간다(BreakerSkill.CurrentRadius).
    //   조준점이 없거나 일시정지 중이면 숨긴다.
    // - 기본: 천천히 돈다.
    // - 타격: Tick 기록마다 링이 빠르게 커졌다 돌아온다(반지름·굵기·밝기). 커지는 반지름은 화면에서만 커진다.
    //   헛친 Tick(맞은 적 없음)은 약하게 튄다. 치명타 Tick은 금색으로 번쩍인다. 빈 Tick(조준점 없음)은 튀지 않는다.
    // - 일시정지 중에는 회전과 튀김을 멈추고, 재개하면 이어간다.
    // 정지 중에는 판이 기록을 비우지 않으므로, 이미 본 Tick은 번호로 걸러 두 번 튀지 않는다.
    internal sealed class BreakerView : IDisposable
    {
        private const int SortingOrder = 10;
        // 사각형이 링의 바깥 가장자리까지 담도록 두는 여유 비율.
        private const float MeshMargin = 1.1f;

        private static readonly int _radiusId = Shader.PropertyToID("_Radius");
        private static readonly int _thicknessId = Shader.PropertyToID("_Thickness");
        private static readonly int _dashCountId = Shader.PropertyToID("_DashCount");
        private static readonly int _dashRatioId = Shader.PropertyToID("_DashRatio");
        private static readonly int _rotationId = Shader.PropertyToID("_Rotation");
        private static readonly int _colorId = Shader.PropertyToID("_Color");
        private static readonly int _flashColorId = Shader.PropertyToID("_FlashColor");
        private static readonly int _flashId = Shader.PropertyToID("_Flash");

        private readonly Transform _root;
        private readonly BreakerLook _look;
        private readonly Mesh _quad;
        private readonly MaterialPropertyBlock _properties = new();
        private readonly Dictionary<PlayerId, Ring> _rings = new();

        private sealed class Ring
        {
            public MeshRenderer Renderer;
            public float Rotation;
            // 사각형의 지금 한 변(월드 단위). 바뀔 때만 transform에 쓴다.
            public float Size;
            public int DrawnTick;
            // 튐이 시작된 뒤 지난 시간. 튐 시간 이상이면 튀지 않는 중이다.
            public float PunchElapsed = float.MaxValue;
            public float PunchStrength;
            public bool PunchCritical;
        }

        public BreakerView(Transform parent, BreakerLook look)
        {
            _root = new GameObject("Breaker View").transform;
            _root.SetParent(parent, false);
            _look = look;
            _quad = CreateQuad();
        }

        public void Synchronize(World world, bool paused, float delta)
        {
            IReadOnlyList<BattlePlayer> players = world.Players;

            // 매 프레임 경로: IReadOnlyList를 인덱스로 돈다(인터페이스 foreach는 열거자를 할당한다).
            for (int i = 0; i < players.Count; i++)
            {
                BattlePlayer player = players[i];

                if (player.Breaker != null)
                    Show(player, player.Breaker, paused, delta);
            }
        }

        // 링이 없고, 지운 객체도 장면에서 모두 사라졌는가.
        // 지운 객체는 프레임 끝에 사라지므로, Reset 뒤 한 프레임이 지나야 true가 된다.
        public bool IsClear => _rings.Count == 0 && _root.childCount == 0;

        // 판이 바뀌거나 판을 정리할 때 모든 링을 지운다.
        public void Reset()
        {
            foreach (Ring ring in _rings.Values)
                Object.Destroy(ring.Renderer.gameObject);

            _rings.Clear();
        }

        public void Dispose()
        {
            Object.Destroy(_root.gameObject);
            Object.Destroy(_quad);
        }

        private void Show(BattlePlayer player, BreakerSkill breaker, bool paused, float delta)
        {
            if (!_rings.TryGetValue(player.Id, out Ring ring))
            {
                ring = Create(player.Id);
                _rings.Add(player.Id, ring);
            }

            if (!paused)
            {
                ring.Rotation = Mathf.Repeat(ring.Rotation + _look.RotationSpeed * delta, 1);
                ring.PunchElapsed += delta;
            }

            ReadTicks(ring, breaker);

            bool visible = !paused && player.AimPoint.HasValue;
            ring.Renderer.enabled = visible;

            if (!visible)
                return;

            Point2 aim = player.AimPoint.Value;
            ring.Renderer.transform.localPosition = new Vector3(aim.X, aim.Y, 0);
            Apply(ring, breaker.CurrentRadius);
        }

        // 새 Tick이 있으면 튐을 처음부터 다시 한다. 한 프레임에 여러 Tick이면 가장 센 튐으로, 하나라도 치명타면 금색으로 튄다.
        private void ReadTicks(Ring ring, BreakerSkill breaker)
        {
            IReadOnlyList<BreakerTick> ticks = breaker.Ticks;
            bool punched = false;

            for (int i = 0; i < ticks.Count; i++)
            {
                BreakerTick tick = ticks[i];

                if (tick.Number <= ring.DrawnTick)
                    continue;

                ring.DrawnTick = tick.Number;

                if (!tick.Center.HasValue)
                    continue;

                float strength = tick.HitCount > 0 ? 1 : _look.MissStrength;

                if (!punched)
                {
                    punched = true;
                    ring.PunchElapsed = 0;
                    ring.PunchStrength = strength;
                    ring.PunchCritical = tick.IsCritical;
                }
                else
                {
                    ring.PunchStrength = Mathf.Max(ring.PunchStrength, strength);
                    ring.PunchCritical |= tick.IsCritical;
                }
            }
        }

        // radius: 판정 반지름(달 버프 포함). 튐으로 커지는 반지름은 화면에서만 커진다.
        private void Apply(Ring ring, float radius)
        {
            float punch = ring.PunchElapsed < _look.PunchDuration
                ? _look.Punch(ring.PunchElapsed / _look.PunchDuration) * ring.PunchStrength
                : 0;
            float shownRadius = radius * (1 + _look.PunchRadiusScale * punch);
            float shownThickness = _look.Thickness * (1 + _look.PunchThicknessScale * punch);

            // 셰이더는 링 중심에서의 월드 거리로 그리므로, 사각형 크기는 링의 크기가 아니라 셰이더가 도는 픽셀 범위만 정한다.
            // 그릴 링이 딱 들어가게 맞춘다. 대부분의 프레임은 크기가 그대로라 쓰지 않는다.
            float size = 2 * (shownRadius + shownThickness * 0.5f) * MeshMargin;

            if (size != ring.Size)
            {
                ring.Size = size;
                ring.Renderer.transform.localScale = new Vector3(size, size, 1);
            }

            ring.Renderer.GetPropertyBlock(_properties);
            _properties.SetFloat(_radiusId, shownRadius);
            _properties.SetFloat(_thicknessId, shownThickness);
            _properties.SetFloat(_dashCountId, _look.DashCount);
            _properties.SetFloat(_dashRatioId, _look.DashRatio);
            _properties.SetFloat(_rotationId, ring.Rotation);
            _properties.SetColor(_colorId, _look.Color);
            _properties.SetColor(_flashColorId, ring.PunchCritical ? _look.CriticalFlashColor : _look.HitFlashColor);
            _properties.SetFloat(_flashId, Mathf.Clamp01(punch));
            ring.Renderer.SetPropertyBlock(_properties);
        }

        // 사각형 크기는 그릴 때마다 Apply가 맞춘다.
        private Ring Create(PlayerId player)
        {
            var view = new GameObject($"Breaker Ring ({player})");
            view.transform.SetParent(_root, false);

            view.AddComponent<MeshFilter>().sharedMesh = _quad;
            var renderer = view.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _look.Material;
            renderer.sortingOrder = SortingOrder;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return new Ring { Renderer = renderer };
        }

        // 한 변이 1인 사각형. 링의 크기는 transform의 배율로 맞춘다.
        private static Mesh CreateQuad()
        {
            var mesh = new Mesh
            {
                name = "Breaker Ring Quad",
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0),
                    new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0),
                },
                triangles = new[] { 0, 2, 1, 2, 3, 1 }
            };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
