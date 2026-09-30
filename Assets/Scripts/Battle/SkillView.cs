using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 스킬의 화면. 매 프레임 판의 참가자를 읽어 그린다. 게임 상태를 바꾸지 않고, 피해를 다시 계산하지 않는다.
    // Breaker는 따로 그린다(BreakerView).
    // - 레이저 예고선: 예고 중인 발사마다 얇은 선. 발사에 가까울수록 진해진다(CONTENT_DEFINITION 5.2).
    // - 레이저 발사선: 발사 기록마다 판정 굵기 그대로의 선이 잠깐 보였다가 옅어진다.
    // 정지 중에는 판이 기록을 비우지 않으므로, 이미 그린 발사는 번호로 걸러 두 번 그리지 않는다.
    internal sealed class SkillView : IDisposable
    {
        private const float TelegraphWidth = 0.04f;
        private const float FireSeconds = 0.2f;
        private static readonly Color TelegraphColor = new Color(1f, 0.9f, 0.3f, 1f);
        private static readonly Color FireColor = new Color(0.35f, 0.9f, 1f, 1f);

        private readonly LineStrokes _strokes;
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
            _telegraphs.Clear();
            _drawnFires.Clear();
        }

        public void Dispose() => _strokes.Dispose();

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
