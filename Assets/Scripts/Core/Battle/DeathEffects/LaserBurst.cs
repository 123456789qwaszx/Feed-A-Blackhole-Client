namespace BlackHole.Core
{
    // 레이저 별 효과가 발동한 기록: 어디서, 어떤 정의로.
    // [후속] 방향·맞힌 적은 레이저 피해를 구현할 때 더한다. 지금은 발동만 기록한다.
    public readonly struct LaserBurst
    {
        // 판 안에서 늘어나는 번호(번개·폭발과 같은 줄).
        public long Sequence { get; }
        public Point2 Origin { get; }
        public LaserBurstDefinition Definition { get; }

        internal LaserBurst(long sequence, Point2 origin, LaserBurstDefinition definition)
        {
            Sequence = sequence;
            Origin = origin;
            Definition = definition;
        }
    }
}
