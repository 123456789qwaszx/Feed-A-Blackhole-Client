namespace BlackHole.Core
{
    // Breaker가 공개하는 업그레이드 수치 이름. 노드의 업그레이드(Upgrade.Stat)가 이 이름으로 판의 Breaker 수치를 보정한다.
    // 업그레이드 시스템은 이 이름을 해석하지 않는다.
    public static class BreakerUpgradeStats
    {
        // 피해. 노드 예: 더하기 1.
        public const string Damage = "breaker.damage";
        // 공격 속도(기본 1). 주기는 기본 주기 ÷ 공격 속도다. 노드 예: 비율 0.25.
        public const string Speed = "breaker.speed";
        // 공격 원의 반지름. 노드 예: 비율 0.1.
        public const string Radius = "breaker.radius";
        // 한 Tick이 치명타일 확률(1을 넘지 않는다). 노드 예: 더하기 0.05.
        public const string CritChance = "breaker.crit-chance";
    }
}
