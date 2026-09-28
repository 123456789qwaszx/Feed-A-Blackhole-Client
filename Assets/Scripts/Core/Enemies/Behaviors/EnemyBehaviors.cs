using System;

namespace BlackHole.Core
{
    // 행동 정의 → 행동 구현. 출현 때 쓰인다.
    internal static class EnemyBehaviors
    {
        public static IEnemyBehavior Create(EnemyBehaviorDefinition definition)
        {
            switch (definition)
            {
                case OrbitBehaviorDefinition orbit:
                    return new OrbitBehavior(orbit);
                default:
                    throw new ArgumentException(
                        $"실행 규칙이 연결되지 않은 행동 종류 '{definition?.GetType().Name}'.", nameof(definition));
            }
        }
    }
}
