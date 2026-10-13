using System;

namespace BlackHole.Core
{
    public readonly struct SupplyRequest
    {
        public EnemyDefinition Enemy { get; }
        public int Count { get; }

        // 재생성(파괴 때 RespawnChance 성공)으로 나온 요청인가. 재생성된 적은 다시 재생성을 굴리지 않는다(사슬 금지).
        public bool Respawn { get; }

        public SupplyRequest(EnemyDefinition enemy, int count, bool respawn = false)
        {
            if (count < 1)
                throw new ArgumentOutOfRangeException(nameof(count), "1 이상의 정수가 필요하다.");

            Enemy = enemy ?? throw new ArgumentNullException(nameof(enemy));
            Count = count;
            Respawn = respawn;
        }
    }
}
