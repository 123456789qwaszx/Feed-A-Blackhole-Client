namespace BlackHole.Core
{
    // 사망이 확정된 순간의 기록. 보상·사망 효과·화면 연출은 이것을 읽는다.
    // 적 객체를 연출이 끝날 때까지 붙잡지 않도록 필요한 값을 복사해 둔다(Gameplay Lifetime ≠ Presentation Lifetime).
    public readonly struct DeathRecord
    {
        // 판 안에서 사망 순서대로 늘어나는 번호. 같은 기록을 두 번 소비하지 않는 데 쓴다.
        public long Sequence { get; }
        public EnemyId EnemyId { get; }
        public string EnemyTypeId { get; }
        public Point2 Position { get; }
        public float Size { get; }

        internal DeathRecord(long sequence, Enemy enemy)
        {
            Sequence = sequence;
            EnemyId = enemy.Id;
            EnemyTypeId = enemy.Definition.Id;
            Position = enemy.Position;
            Size = enemy.Stats.Size;
        }
    }
}
