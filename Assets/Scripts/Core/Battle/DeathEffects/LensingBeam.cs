namespace BlackHole.Core
{
    // 지금 날아가는 중력 굴절광 빛무리 하나의 상태.
    // 경로는 Start(죽은 자리) → Control(바깥쪽으로 비스듬히 뻗은 조절점) → End(블랙홀 중심)의 2차 베지어 곡선이고, Progress(0 ~ 1)가 머리의 위치다.
    // 화면은 이것을 읽어 머리 뒤로 꼬리를 그릴 뿐 피해를 다시 계산하지 않는다(같은 곡선은 PointAt).
    // 처리(DeathEffects.Resolve)마다 지금 상태로 다시 채운다. 머리가 끝(블랙홀)에 닿은 빛무리는 목록에서 빠진다.
    public readonly struct LensingBeam
    {
        public long Sequence { get; }  // 판 안에서 늘어나는 번호(번개·폭발·레이저와 같은 줄). 화면이 같은 빛무리를 알아보는 데 쓴다.
        public Point2 Start { get; }
        public Point2 Control { get; }
        public Point2 End { get; }
        public float Progress { get; }  // 머리의 곡선 위치(0 = 시작, 1 = 블랙홀).
        public float Width { get; }     // 판정 너비.
        public float Duration { get; }  // 머리가 시작에서 끝까지 가는 시간(초). 화면이 꼬리를 흘리는 데 쓴다.
        public int HitCount { get; }    // 지금까지 피해를 준 적의 수.

        internal LensingBeam(long sequence, Point2 start, Point2 control, Point2 end, float progress, float width, float duration, int hitCount)
        {
            Sequence = sequence;
            Start = start;
            Control = control;
            End = end;
            Progress = progress;
            Width = width;
            Duration = duration;
            HitCount = hitCount;
        }

        // 곡선 위의 점(t = 0 ~ 1).
        public Point2 PointAt(float t) => PointOn(Start, Control, End, t);

        internal static Point2 PointOn(Point2 start, Point2 control, Point2 end, float t)
        {
            float u = 1 - t;
            float a = u * u;
            float b = 2 * u * t;
            float c = t * t;

            return new Point2(
                a * start.X + b * control.X + c * end.X,
                a * start.Y + b * control.Y + c * end.Y);
        }
    }
}
