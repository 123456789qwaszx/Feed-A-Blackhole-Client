using System;

namespace BlackHole.Core
{
    internal static class EnemyBehaviors
    {
        public static Point2 NextPosition(Point2 position, EnemyStats stats, float delta)
        {
            Point2 center = BattleSpace.Origin;
            float dx = position.X - center.X;
            float dy = position.Y - center.Y;
            float radius = (float)Math.Sqrt(dx * dx + dy * dy);

            if (radius == 0)
                return position;

            float angle = (float)Math.Atan2(dy, dx);
            float turn = stats.MoveSpeed / radius * delta;

            return new Point2(
                center.X + radius * (float)Math.Cos(angle + turn),
                center.Y + radius * (float)Math.Sin(angle + turn));
        }
    }
}
