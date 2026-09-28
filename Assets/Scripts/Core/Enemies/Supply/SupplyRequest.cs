using System;

namespace BlackHole.Core
{
    public readonly struct SupplyRequest
    {
        public EnemyDefinition Enemy { get; }
        public int Count { get; }

        public SupplyRequest(EnemyDefinition enemy, int count)
        {
            if (count < 1)
                throw new ArgumentOutOfRangeException(nameof(count), "1 이상의 정수가 필요하다.");

            Enemy = enemy ?? throw new ArgumentNullException(nameof(enemy));
            Count = count;
        }
    }
}
