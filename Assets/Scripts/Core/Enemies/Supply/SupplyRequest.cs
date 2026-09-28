using System;

namespace BlackHole.Core
{
    // 적 공급 한 건: 어떤 종류를 몇 마리. 무엇을 얼마나는 공급이, 어디에는 배치가 정한다(SYSTEM_CATALOG S08).
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
