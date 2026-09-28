namespace BlackHole.Core
{
    // 발사 하나의 기록. 화면은 이것을 읽어 그릴 뿐 피해를 다시 계산하지 않는다.
    public readonly struct LaserFire
    {
        // 발사한 예고의 번호. 같은 발사를 두 번 그리지 않는 데 쓴다.
        public int Number { get; }
        public Point2 Start { get; }
        public Point2 End { get; }
        public float Width { get; }
        // 피해를 준 적의 수.
        public int HitCount { get; }

        internal LaserFire(LaserShot shot, float width, int hitCount)
        {
            Number = shot.Number;
            Start = shot.Start;
            End = shot.End;
            Width = width;
            HitCount = hitCount;
        }
    }
}
