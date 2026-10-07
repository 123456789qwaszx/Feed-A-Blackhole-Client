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
        // 붙어 있던 특수 성질의 ID. 없으면 null.
        public string TraitId { get; }
        // 죽은 적의 색 등급과 크기(SizeRule). 파편 수 같은 연출이 쓴다. 픽업의 크기는 SizeRule.Base다.
        public int Tier { get; }
        public int Size { get; }
        public Point2 Position { get; }
        public float Radius { get; }

        internal DeathRecord(long sequence, Enemy enemy)
        {
            Sequence = sequence;
            EnemyId = enemy.Id;
            EnemyTypeId = enemy.Definition.Id;
            TraitId = enemy.Trait?.Id;
            Tier = enemy.Tier;
            Size = enemy.Size;
            Position = enemy.Position;
            Radius = enemy.Stats.Radius;
        }
    }
}
