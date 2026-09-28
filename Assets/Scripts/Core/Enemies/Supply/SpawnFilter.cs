namespace BlackHole.Core
{
    // 개체 수 상한(콘텐츠의 MaxAliveEnemies) 체크.
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
