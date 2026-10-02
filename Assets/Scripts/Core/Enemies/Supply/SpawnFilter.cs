namespace BlackHole.Core
{
    // 개체 수 상한(콘텐츠의 MaxAliveEnemies) 체크. 상한은 공급된 적에만 건다 — 픽업은 세지 않는다(World).
    internal sealed class SpawnFilter
    {
        private readonly int _maxAliveEnemies;

        public SpawnFilter(int maxAliveEnemies)
        {
            _maxAliveEnemies = maxAliveEnemies;
        }

        public bool Allows(int suppliedAlive) => suppliedAlive < _maxAliveEnemies;
    }
}
