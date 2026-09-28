using System;

namespace BlackHole.Core
{
    // 출현 위치의 공유 정의: HQ(원점)를 둘러싼 원형 띠. 적은 띠 안의 무작위 지점에 나온다.
    // 띠 안에서 넓이가 고르게 퍼지도록 뽑는다(안쪽 가장자리에 몰리지 않는다).
    // 겹침 방지와 전체 개체 수 상한은 아직 없다(SYSTEM_CATALOG S08의 남은 결정).
    public sealed class EnemyPlacementDefinition
    {
        public float MinDistance { get; }
        public float MaxDistance { get; }

        public EnemyPlacementDefinition(float minDistance, float maxDistance)
        {
            if (float.IsNaN(minDistance) || float.IsInfinity(minDistance) || minDistance < 0)
                throw new ArgumentOutOfRangeException(nameof(minDistance), "0 이상의 유한한 값이 필요하다.");

            MaxDistance = DefinitionGuard.Positive(maxDistance, nameof(maxDistance));

            if (minDistance > maxDistance)
                throw new ArgumentOutOfRangeException(nameof(minDistance), "최대 거리보다 클 수 없다.");

            MinDistance = minDistance;
        }

        internal Point2 Pick(BattleRandom random)
        {
            float angle = random.NextFloat() * 2 * (float)Math.PI;
            float min2 = MinDistance * MinDistance;
            float max2 = MaxDistance * MaxDistance;
            float distance = (float)Math.Sqrt(min2 + random.NextFloat() * (max2 - min2));
            Point2 center = BattleSpace.Origin;

            return new Point2(
                center.X + distance * (float)Math.Cos(angle),
                center.Y + distance * (float)Math.Sin(angle));
        }
    }
}
