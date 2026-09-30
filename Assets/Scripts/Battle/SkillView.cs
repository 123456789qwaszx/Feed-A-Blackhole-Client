using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 스킬의 화면. 매 프레임 판의 참가자를 읽어 그린다. 게임 상태를 바꾸지 않고, 피해를 다시 계산하지 않는다.
    // - Breaker 범위 원: 참가자의 조준점에 Breaker 반지름으로 늘 그린다. 판정과 같은 값이다(GAME_RULES 6절). 조준점이 없으면 숨긴다.
    // - Breaker Tick 원: Tick 기록마다 한 번 굵게 그렸다가 옅어진다. 치명타 Tick은 금색이다. 빈 Tick(조준점 없음)은 그리지 않는다.
    // - 레이저 예고선: 예고 중인 발사마다 얇은 선. 발사에 가까울수록 진해진다(CONTENT_DEFINITION 5.2).
    // - 레이저 발사선: 발사 기록마다 예고선 굵기에서 판정 굵기로 순식간에 굵어졌다가 옅어진다. 예고선과 같은 단색이다.
    // 정지 중에는 판이 기록을 비우지 않으므로, 이미 그린 Tick·발사는 번호로 걸러 두 번 그리지 않는다.
    internal sealed class SkillView : IDisposable
    {
        private const float RangeWidth = 0.03f;
        private const float TickWidth = 0.08f;
        private const float TickSeconds = 0.3f;
        private const float TelegraphWidth = 0.04f;
        // 발사선이 예고선 굵기에서 판정 굵기까지 굵어지는 시간.
        private const float FireGrowSeconds = 0.05f;
        // 발사선이 보이는 시간(굵어지는 시간 포함). 굵어진 뒤 남은 시간 동안 옅어진다.
        private const float FireSeconds = 0.2f;
        private static readonly Color BreakerColor = new Color(0.3f, 1f, 0.55f, 1f);
        private static readonly Color CriticalColor = new Color(1f, 0.85f, 0.2f, 1f);
        private static readonly Color RangeColor = new Color(0.3f, 1f, 0.55f, 0.45f);
        // 예고선과 발사선의 색. 얇은 예고선이 그대로 굵어지는 것처럼 보이도록 같은 색을 쓴다.
        private static readonly Color LaserColor = new Color(1f, 0.9f, 0.3f, 1f);

        private readonly LineStrokes _strokes;
        private readonly Dictionary<PlayerId, LineRenderer> _ranges = new Dictionary<PlayerId, LineRenderer>();
        private readonly Dictionary<PlayerId, int> _drawnTicks = new Dictionary<PlayerId, int>();
        private readonly Dictionary<PlayerId, int> _drawnFires = new Dictionary<PlayerId, int>();
        // 예고선. 이번 프레임에 쓰지 않은 선은 숨겨 두었다가 다음 예고에 다시 쓴다.
        private readonly List<LineRenderer> _telegraphs = new List<LineRenderer>();
        // 발사선. 다 보인 선은 숨겨 두었다가 다음 발사에 다시 쓴다.
        private readonly List<Beam> _beams = new List<Beam>();

        private sealed class Beam
        {
            public LineRenderer Line;
            // 판정 굵기.
            public float Width;
            public float Elapsed;
        }

        public SkillView(Transform parent) => _strokes = new LineStrokes(parent, "Skill View");

        public void Synchronize(World world, float delta)
        {
            _strokes.Age(delta);
            AgeBeams(delta);
            int telegraphs = 0;
            IReadOnlyList<BattlePlayer> players = world.Players;

            // 매 프레임 경로: IReadOnlyList를 인덱스로 돈다(인터페이스 foreach는 열거자를 할당한다).
            for (int i = 0; i < players.Count; i++)
            {
                BattlePlayer player = players[i];

                if (player.Breaker != null)
                    ShowBreaker(player, player.Breaker);

                if (player.Laser != null)
                    telegraphs = ShowLaser(player.Id, player.Laser, telegraphs);
            }

            for (int i = telegraphs; i < _telegraphs.Count; i++)
                _telegraphs[i].enabled = false;
        }

        // 그리는 선이 없고, 지운 객체도 장면에서 모두 사라졌는가(Reset 뒤 한 프레임).
        public bool IsClear => _strokes.IsClear;

        // 판이 바뀌거나 판을 정리할 때 모든 선을 지운다.
        public void Reset()
        {
            _strokes.Reset();
            _ranges.Clear();
            _telegraphs.Clear();
            _beams.Clear();
            _drawnTicks.Clear();
            _drawnFires.Clear();
        }

        public void Dispose() => _strokes.Dispose();

        private void ShowBreaker(BattlePlayer player, BreakerSkill breaker)
        {
            if (!_ranges.TryGetValue(player.Id, out LineRenderer range))
            {
                range = _strokes.Line($"Breaker Range ({player.Id})", RangeWidth, RangeColor);
                LineStrokes.SetCircle(range, BattleSpace.Origin, breaker.Definition.Radius);
                _ranges.Add(player.Id, range);
            }

            range.enabled = player.AimPoint.HasValue;

            if (player.AimPoint.HasValue)
                LineStrokes.MoveTo(range, player.AimPoint.Value);

            _drawnTicks.TryGetValue(player.Id, out int drawn);
            IReadOnlyList<BreakerTick> ticks = breaker.Ticks;

            for (int i = 0; i < ticks.Count; i++)
            {
                BreakerTick tick = ticks[i];

                if (tick.Number <= drawn)
                    continue;

                drawn = tick.Number;

                if (tick.Center.HasValue)
                {
                    Color color = tick.IsCritical ? CriticalColor : BreakerColor;
                    LineStrokes.SetCircle(_strokes.Flash("Breaker Tick", TickWidth, color, TickSeconds), tick.Center.Value, tick.Radius);
                }
            }

            _drawnTicks[player.Id] = drawn;
        }

        // 예고선을 used번째부터 채우고, 다음에 쓸 예고선 번호를 돌려준다.
        private int ShowLaser(PlayerId player, LaserSkill laser, int used)
        {
            IReadOnlyList<LaserShot> pending = laser.PendingShots;

            for (int i = 0; i < pending.Count; i++)
            {
                LaserShot shot = pending[i];

                if (used == _telegraphs.Count)
                    _telegraphs.Add(_strokes.Line("Laser Telegraph", TelegraphWidth, LaserColor));

                LineRenderer line = _telegraphs[used++];
                LineStrokes.SetSegment(line, shot.Start, shot.End);
                Color color = LaserColor;
                color.a = Mathf.Lerp(0.25f, 1f, 1 - Mathf.Clamp01(shot.Remaining / laser.Definition.TelegraphDuration));
                LineStrokes.SetColor(line, color);
                line.enabled = true;
            }

            _drawnFires.TryGetValue(player, out int drawn);
            IReadOnlyList<LaserFire> fires = laser.Fires;

            for (int i = 0; i < fires.Count; i++)
            {
                LaserFire fire = fires[i];

                if (fire.Number <= drawn)
                    continue;

                drawn = fire.Number;
                Fire(fire.Start, fire.End, fire.Width);
            }

            _drawnFires[player] = drawn;
            return used;
        }

        // 발사선 하나를 예고선 굵기로 시작한다. 쉬는 발사선이 있으면 다시 쓴다.
        private void Fire(Point2 start, Point2 end, float width)
        {
            Beam beam = null;

            foreach (Beam candidate in _beams)
            {
                if (candidate.Elapsed >= FireSeconds)
                {
                    beam = candidate;
                    break;
                }
            }

            if (beam == null)
            {
                beam = new Beam { Line = _strokes.Line("Laser Fire", TelegraphWidth, LaserColor) };
                _beams.Add(beam);
            }

            beam.Width = width;
            beam.Elapsed = 0;
            LineStrokes.SetSegment(beam.Line, start, end);
            ShowBeam(beam);
        }

        // 굵어지는 동안은 진하게, 다 굵어진 뒤에는 옅어진다. 다 보인 선은 숨긴다.
        private void AgeBeams(float delta)
        {
            foreach (Beam beam in _beams)
            {
                if (beam.Elapsed >= FireSeconds)
                    continue;

                beam.Elapsed += delta;
                ShowBeam(beam);
            }
        }

        private void ShowBeam(Beam beam)
        {
            beam.Line.enabled = beam.Elapsed < FireSeconds;

            if (!beam.Line.enabled)
                return;

            float grow = Mathf.Clamp01(beam.Elapsed / FireGrowSeconds);
            float fade = Mathf.Clamp01((beam.Elapsed - FireGrowSeconds) / (FireSeconds - FireGrowSeconds));
            beam.Line.widthMultiplier = Mathf.Lerp(TelegraphWidth, beam.Width, grow);

            Color color = LaserColor;
            color.a = 1 - fade;
            LineStrokes.SetColor(beam.Line, color);
        }
    }
}
