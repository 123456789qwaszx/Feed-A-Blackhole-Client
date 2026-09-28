namespace BlackHole.Core
{
    // 판 안의 참가자 하나: 조준점과 스킬. 판 조립 때 참가자마다 새로 만들고 판이 끝나면 버린다.
    // 전투 사이에 남는 진행 상태(Gold·산 노드)는 PlayerState다. 캐릭터가 아니다 — 판 안에서 움직이는 몸이 없다.
    // 모든 참가자가 콘텐츠의 스킬을 받는다. Breaker 수치는 판 조립이 그 참가자의 업그레이드 표로 계산한 값이다(노드로 스킬을 여는 규칙은 없다).
    // 스킬은 콘텐츠 순서(Breaker → 레이저)로 공격한다. 두 스킬을 묶는 공통 형식은 두지 않는다.
    public sealed class BattlePlayer
    {
        public PlayerId Id { get; }
        // 이 참가자의 조준점. 누가 채우는지는 모른다 — 지금은 호스트가 마우스 위치로 채운다.
        // 참가자가 마우스를 가진다는 뜻이 아니다. 없으면 null.
        public Point2? AimPoint { get; private set; }
        // 콘텐츠에 그 스킬이 없으면 null.
        public BreakerSkill Breaker { get; }
        public LaserSkill Laser { get; }

        // seed는 판의 seed다. 레이저(시작점)와 Breaker(치명타)는 여기서 자기만 쓰는 난수를 받는다.
        internal BattlePlayer(PlayerId id, BreakerDefinition breaker, LaserDefinition laser, int seed)
        {
            Id = id;
            Breaker = breaker != null ? new BreakerSkill(breaker, BattleRandom.ForPlayer(seed, BattleRandom.CriticalStream, id)) : null;
            Laser = laser != null ? new LaserSkill(laser, BattleRandom.ForPlayer(seed, BattleRandom.LaserStream, id)) : null;
        }

        internal void SetAimPoint(Point2? aimPoint) => AimPoint = aimPoint;

        internal void BeginAdvance()
        {
            Breaker?.BeginAdvance();
            Laser?.BeginAdvance();
        }

        // World.Step의 2. Passive Attack 자리에서 부른다.
        internal void Attack(float delta, World world)
        {
            Breaker?.Advance(delta, this, world);
            Laser?.Advance(delta, this, world);
        }
    }
}
