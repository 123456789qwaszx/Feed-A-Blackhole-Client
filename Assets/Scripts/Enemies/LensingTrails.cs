using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 중력 굴절광 빛무리를 그리는 부품 [임시]: 머리(판의 진행 위치)에서 곡선을 따라 뒤로 꼬리를 늘인 선 하나.
    // 꼬리는 투명하고 가늘며, 머리 쪽이 밝고 판정 너비만큼 굵다. 곡선은 판의 기록과 같은 것(LensingBeam.PointAt)을 쓴다.
    // 사망 효과 화면(DeathEffectView)이 매 프레임 판의 빛무리 목록(DeathEffects.LensingBeams)을 넘긴다.
    // 머리가 블랙홀에 닿아 목록에서 빠진 빛무리는 꼬리가 마저 블랙홀로 빨려 들어간 뒤 숨긴다. 피해를 다시 계산하지 않는다.
    internal sealed class LensingTrails : IDisposable
    {
        // 꼬리 길이(곡선 매개변수 기준, 0 ~ 1).
        private const float TrailLength = 0.35f;
        private const int Points = 28;
        private const float TailWidthRatio = 0.15f;

        private static readonly Color HeadColor = new(0.85f, 0.97f, 1f, 1f);
        private static readonly Color TailColor = new(0.55f, 0.75f, 1f, 0f);

        private readonly LineStrokes _strokes;
        private readonly List<Trail> _trails = new();
        private readonly Vector3[] _positions = new Vector3[Points];

        private sealed class Trail
        {
            public long Sequence;
            public LineRenderer Line;
            public LensingBeam Beam;
            public float Head;
            public float Tail;
            // 판의 목록에서 빠졌다(머리가 블랙홀에 닿았다). 꼬리만 마저 흘려 보낸다.
            public bool Draining;
            public bool Active;
        }

        public LensingTrails(Transform parent) => _strokes = new LineStrokes(parent, "Lensing Beams");

        // 새로 보인 빛무리 수를 돌려준다(소리용).
        public int Show(IReadOnlyList<LensingBeam> beams, bool paused, float delta)
        {
            int started = 0;

            for (int i = 0; i < _trails.Count; i++)
            {
                if (_trails[i].Active)
                    _trails[i].Draining = true;
            }

            for (int i = 0; i < beams.Count; i++)
            {
                LensingBeam beam = beams[i];
                Trail trail = Find(beam.Sequence);

                if (trail == null)
                {
                    trail = Take(beam.Sequence);
                    started++;
                }

                trail.Beam = beam;
                trail.Draining = false;
                trail.Head = beam.Progress;
                trail.Tail = Mathf.Max(0, beam.Progress - TrailLength);
            }

            for (int i = 0; i < _trails.Count; i++)
            {
                Trail trail = _trails[i];

                if (!trail.Active)
                    continue;

                if (trail.Draining)
                {
                    trail.Head = 1;

                    if (!paused && trail.Beam.Duration > 0)
                        trail.Tail += delta / trail.Beam.Duration;

                    if (trail.Tail >= 1)
                    {
                        trail.Active = false;
                        trail.Sequence = 0;
                        trail.Line.enabled = false;
                        continue;
                    }
                }

                Draw(trail);
            }

            return started;
        }

        public void Reset()
        {
            _strokes.Reset();
            _trails.Clear();
        }

        public void Dispose() => _strokes.Dispose();

        private void Draw(Trail trail)
        {
            if (trail.Head - trail.Tail < 0.001f)
            {
                trail.Line.enabled = false;
                return;
            }

            for (int i = 0; i < Points; i++)
            {
                float t = Mathf.Lerp(trail.Tail, trail.Head, (float)i / (Points - 1));
                Point2 point = trail.Beam.PointAt(t);
                _positions[i] = new Vector3(point.X, point.Y, 0);
            }

            LineRenderer line = trail.Line;
            line.transform.localPosition = Vector3.zero;
            line.loop = false;
            line.positionCount = Points;
            line.SetPositions(_positions);
            line.widthMultiplier = trail.Beam.Width;
            line.startColor = TailColor;
            line.endColor = HeadColor;
            line.enabled = true;
        }

        private Trail Find(long sequence)
        {
            for (int i = 0; i < _trails.Count; i++)
            {
                if (_trails[i].Active && _trails[i].Sequence == sequence)
                    return _trails[i];
            }

            return null;
        }

        // 쉬는 선을 먼저 쓰고, 없으면 새로 만든다. 선의 굵기는 꼬리가 가늘고 머리가 굵다(너비 배율은 Draw가 맞춘다).
        private Trail Take(long sequence)
        {
            Trail trail = null;

            for (int i = 0; i < _trails.Count; i++)
            {
                if (!_trails[i].Active)
                {
                    trail = _trails[i];
                    break;
                }
            }

            if (trail == null)
            {
                LineRenderer line = _strokes.Line("Lensing Beam", 1, HeadColor);
                line.widthCurve = AnimationCurve.Linear(0, TailWidthRatio, 1, 1);
                line.numCapVertices = 4;
                trail = new Trail { Line = line };
                _trails.Add(trail);
            }

            trail.Sequence = sequence;
            trail.Active = true;
            trail.Draining = false;
            return trail;
        }
    }
}
