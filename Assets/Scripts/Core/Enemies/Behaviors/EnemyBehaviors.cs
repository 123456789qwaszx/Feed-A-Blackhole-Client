using System;

namespace BlackHole.Core
{
    // 적의 움직임. 지금 게임에 있는 행동은 HQ(원점) 주위를 도는 공전 하나다(GAME_RULES 7절).
    // 원점으로부터의 거리는 유지하고, 이동 속도만큼 원 둘레를 따라 움직인다.
    // [임시] "거리 유지"는 "HQ를 중심으로 공전한다"(GAME_RULES 7절)를 가장 단순하게 읽은 해석이다.
    // 상태 기계나 행동 전환은 만들지 않는다(GAME_RULES 7절). 두 번째 행동이 기획되면 그때 나눈다.
    internal static class EnemyBehaviors
    {
        public static Point2 NextPosition(OrbitBehaviorDefinition orbit, Point2 position, EnemyStats stats, float delta)
        {
            Point2 center = BattleSpace.Origin;
            float dx = position.X - center.X;
            float dy = position.Y - center.Y;
            float radius = (float)Math.Sqrt(dx * dx + dy * dy);

            if (radius == 0)
                return position;

            float angle = (float)Math.Atan2(dy, dx);
            float turn = stats.MoveSpeed / radius * delta * (orbit.Clockwise ? -1 : 1);

            return new Point2(
                center.X + radius * (float)Math.Cos(angle + turn),
                center.Y + radius * (float)Math.Sin(angle + turn));
        }
    }
}
