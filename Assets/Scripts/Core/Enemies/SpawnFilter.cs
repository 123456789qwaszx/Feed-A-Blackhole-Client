namespace BlackHole.Core
{
    // 생성 여과 장치: 공급이 요청한 적 하나가 이 판에 실제로 나올 수 있는지 정한다. 판마다 하나 있다.
    // 보는 것은 하나다: 살아 있는 적 전체가 전체 개체 수 상한(콘텐츠의 MaxAliveEnemies)보다 적은가. 성능 예산이며,
    // 공급 계기와 노드가 무엇을 약속하든 판의 적 수는 이 수를 넘지 않는다(성능 원칙 §1·§5·§7).
    // 어떤 종류로 나오는가(변환·특수 확률)와 색은 여과를 통과한 뒤 몫 방식으로 정한다(World). 그래서 거른 요청은 몫을 쓰지 않는다.
    // 거른 요청은 버린다 — 나중에 자리가 나도 다시 나오지 않는다(공급은 사건마다 유한하다).
    internal sealed class SpawnFilter
    {
        private readonly int _maxAliveEnemies;

        public SpawnFilter(int maxAliveEnemies)
        {
            _maxAliveEnemies = maxAliveEnemies;
        }

        public bool Allows(EnemyRoster roster) => roster.Alive.Count < _maxAliveEnemies;
    }
}
