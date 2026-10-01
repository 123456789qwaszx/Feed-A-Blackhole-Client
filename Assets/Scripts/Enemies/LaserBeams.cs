using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    internal sealed class LaserBeams : IDisposable
    {
        private const float StartWidth = 0.04f;  // 예고선 굵기.
        private const float GrowSeconds = 0.05f; // 굵어지는 시간

        // 발사선이 보이는 시간(굵어지는 시간 포함). 굵어진 뒤 남은 시간 동안 옅어진다.
        private const float TotalSeconds = 0.2f;

        private static readonly Color BeamColor = new(1f, 0.9f, 0.3f, 1f);

        private readonly LineStrokes _strokes;
        private readonly List<Beam> _beams = new();

        private sealed class Beam
        {
            public LineRenderer Line;
            public float Width; // 판정 굵기.
            public float Elapsed;
            public bool Playing;
        }

        public LaserBeams(Transform parent) => _strokes = new LineStrokes(parent, "Laser Beams");

        public bool IsClear => _strokes.IsClear;

        // width: 판정 너비(LaserBurstDefinition.Width). 다 굵어진 굵기와 맞는 범위가 같음.
        public void Play(Point2 start, Point2 end, float width)
        {
            Beam beam = Take();
            beam.Width = width;
            beam.Elapsed = 0;
            beam.Playing = true;
            LineStrokes.SetSegment(beam.Line, start, end);
            Show(beam);
        }

        public void Age(float delta)
        {
            // 매 프레임 경로: 인덱스로 돈다.
            for (int i = 0; i < _beams.Count; i++)
            {
                Beam beam = _beams[i];

                if (!beam.Playing)
                    continue;

                beam.Elapsed += delta;

                if (beam.Elapsed >= TotalSeconds)
                {
                    beam.Playing = false;
                    beam.Line.enabled = false;
                    continue;
                }

                Show(beam);
            }
        }

        public void Reset()
        {
            _strokes.Reset();
            _beams.Clear();
        }

        public void Dispose() => _strokes.Dispose();

        // 쉬는 선을 먼저 쓰고, 없으면 새로 만든다.
        private Beam Take()
        {
            for (int i = 0; i < _beams.Count; i++)
            {
                if (!_beams[i].Playing)
                    return _beams[i];
            }

            var beam = new Beam { Line = _strokes.Line("Laser Beam", StartWidth, BeamColor) };
            _beams.Add(beam);
            return beam;
        }

        private void Show(Beam beam)
        {
            float grow = Mathf.Clamp01(beam.Elapsed / GrowSeconds);
            float fade = Mathf.Clamp01((beam.Elapsed - GrowSeconds) / (TotalSeconds - GrowSeconds));
            beam.Line.widthMultiplier = Mathf.Lerp(StartWidth, beam.Width, grow);

            Color color = BeamColor;
            color.a = 1 - fade;
            LineStrokes.SetColor(beam.Line, color);
            beam.Line.enabled = true;
        }
    }
}
