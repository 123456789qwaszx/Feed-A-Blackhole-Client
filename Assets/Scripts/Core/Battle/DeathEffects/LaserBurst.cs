namespace BlackHole.Core
{
    // 레이저 별 효과가 발동한 기록: 어디서, 어떤 정의로, 방향, 맞힌 적의 수 등
    public readonly struct LaserBurst
    {
        public long Sequence { get; }  // 판 안에서 늘어나는 번호(번개·폭발과 같은 줄).
        public Point2 Origin { get; }  // 레이저가 시작한 자리(레이저 별이 죽은 자리).
        public Point2 End { get; }     // 레이저가 끝난 자리. 방향은 Origin -> End.
        public LaserBurstDefinition Definition { get; } // 이 판의 정의(너비, 피해 등).
        public bool Critical { get; }
        public int HitCount { get; } // 피해를 준 적의 수.

        internal LaserBurst(
            long sequence,
            Point2 origin,
            Point2 end,
            LaserBurstDefinition definition,
            bool critical,
            int hitCount)
        {
            Sequence = sequence;
            Origin = origin;
            End = end;
            Definition = definition;
            Critical = critical;
            HitCount = hitCount;
        }
    }
}
