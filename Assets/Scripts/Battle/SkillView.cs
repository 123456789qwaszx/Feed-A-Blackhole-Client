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
    // - 레이저 발사선: 발사 기록마다 판정 굵기 그대로의 선이 잠깐 보였다가 옅어진다.
    // 정지 중에는 판이 기록을 비우지 않으므로, 이미 그린 Tick·발사는 번호로 걸러 두 번 그리지 않는다.
    internal sealed class SkillView : IDisposable
    {
        private const float RangeWidth = 0.03f;
        private const float TickWidth = 0.08f;
        private const float TickSeconds = 0.3f;
        private const float TelegraphWidth = 0.04f;
        private const float FireSeconds = 0.2f;
        private static readonly Color BreakerColor = new Color(0.3f, 1f, 0.55f, 1f);
        private static readonly Color CriticalColor = new Color(1f, 0.85f, 0.2f, 1f);
        private static readonly Color RangeColor = new Color(0.3f, 1f, 0.55f, 0.45f);
        private static readonly Color TelegraphColor = new Color(1f, 0.9f, 0.3f, 1f);
        private static readonly Color FireColor = new Color(0.35f, 0.9f, 1f, 1f);

        private readonly LineStrokes _strokes;
        private readonly Dictionary<PlayerId, LineRenderer> _ranges = new Dictionary<PlayerId, LineRenderer>();
        private readonly Dictionary<PlayerId, int> _drawnTicks = new Dictionary<PlayerId, int>();
        private readonly Dictionary<PlayerId, int> _drawnFires = new Dictionary<PlayerId, int>();
        // 예고선. 이번 프레임에 쓰지 않은 선은 숨겨 두었다가 다음 예고에 다시 쓴다.
        private readonly List<LineRenderer> _telegraphs = new List<LineRenderer>();

        public SkillView(Transform parent) => _strokes = new LineStrokes(parent, "Skill View");

        public void Synchronize(World world, float delta)
        {
            _strokes.Age(delta);
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
                    _telegraphs.Add(_strokes.Line("Laser Telegraph", TelegraphWidth, TelegraphColor));

                LineRenderer line = _telegraphs[used++];
                LineStrokes.SetSegment(line, shot.Start, shot.End);
                Color color = TelegraphColor;
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
                LineStrokes.SetSegment(_strokes.Flash("Laser Fire", fire.Width, FireColor, FireSeconds), fire.Start, fire.End);
            }

            _drawnFires[player] = drawn;
            return used;
        }
    }
}
