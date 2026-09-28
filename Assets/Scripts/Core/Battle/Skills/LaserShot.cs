namespace BlackHole.Core
{
    // 예고 중인 발사 하나. 경로는 예고를 시작할 때 정해지고 바뀌지 않는다.
    public readonly struct LaserShot
    {
        // 이 레이저의 몇 번째 예고인가(1부터).
        public int Number { get; }
        // 경계 원 위의 시작점.
        public Point2 Start { get; }
        // 시작점에서 조준점을 지나 경계 원의 반대편에 닿는 점.
        public Point2 End { get; }
        // 발사까지 남은 시간(초).
        public float Remaining { get; }

        internal LaserShot(int number, Point2 start, Point2 end, float remaining)
        {
            Number = number;
            Start = start;
            End = end;
            Remaining = remaining;
        }

        internal LaserShot Elapse(float delta) => new LaserShot(Number, Start, End, Remaining - delta);
    }
}
