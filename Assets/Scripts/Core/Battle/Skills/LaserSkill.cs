using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    public sealed class LaserSkill
    {
        private const float TimeEpsilon = 1e-5f;

        private readonly BattleRandom _random;
        private readonly List<LaserShot> _pending = new();
        private readonly List<LaserFire> _fires = new();
        private readonly List<Enemy> _targets = new();
        private float _untilNextTelegraph;
        private int _telegraphCount;

        public LaserDefinition Definition { get; }

        public bool Enabled { get; private set; } = true;

        // 예고 중인 발사(예고한 순서). 화면은 이것으로 예고선을 그린다.
        public IReadOnlyList<LaserShot> PendingShots { get; }

        // 마지막 진행 동안의 발사(발사한 순서). 다음 진행이 시작될 때 비운다.
        public IReadOnlyList<LaserFire> Fires { get; }

        // 지금까지 발사한 수.
        public int FireCount { get; private set; }

        // random은 이 레이저만 쓰는 난수다. 다른 곳이 난수를 뽑는 횟수가 시작점의 순서를 바꾸지 않는다.
        internal LaserSkill(LaserDefinition definition, BattleRandom random)
        {
            Definition = definition;
            _random = random;
            PendingShots = _pending.AsReadOnly();
            Fires = _fires.AsReadOnly();
        }

        // 끄면 돌던 주기와 예고 중인 발사를 버린다(발사하지 않는다). 다시 켜면 처음부터 돈다: 켠 뒤 첫 Step에 첫 예고.
        public void SetEnabled(bool enabled)
        {
            if (Enabled == enabled)
                return;

            Enabled = enabled;
            _untilNextTelegraph = 0;
            _pending.Clear();
        }

        internal void BeginAdvance() => _fires.Clear();

        internal void Advance(float delta, BattlePlayer owner, World world)
        {
            if (!Enabled)
                return;

            for (int i = 0; i < _pending.Count; i++)
                _pending[i] = _pending[i].Elapse(delta);

            _untilNextTelegraph -= delta;

            // 예고 시각이 이 Step 안에서 지나갔다면 그만큼 예고도 이미 진행된 것이다.
            while (_untilNextTelegraph <= TimeEpsilon)
            {
                Telegraph(owner.AimPoint, Definition.TelegraphDuration + _untilNextTelegraph);
                _untilNextTelegraph += Definition.Interval;
            }

            for (int i = 0; i < _pending.Count;)
            {
                if (_pending[i].Remaining > TimeEpsilon)
                {
                    i++;
                    continue;
                }

                LaserShot shot = _pending[i];
                _pending.RemoveAt(i);
                Fire(shot, owner, world);
            }
        }

        private void Telegraph(Point2? aim, float remaining)
        {
            float radius = Definition.BoundaryRadius;

            if (!aim.HasValue || aim.Value.DistanceSquared(BattleSpace.Origin) >= radius * radius)
                return;

            double angle = _random.NextFloat() * 2 * Math.PI;
            var start = new Point2(
                BattleSpace.Origin.X + radius * (float)Math.Cos(angle),
                BattleSpace.Origin.Y + radius * (float)Math.Sin(angle));

            // 시작점에서 조준점 쪽으로 가는 직선이 원의 반대편과 만나는 점.
            float dx = aim.Value.X - start.X;
            float dy = aim.Value.Y - start.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            dx /= length;
            dy /= length;
            float travel = -2 * ((start.X - BattleSpace.Origin.X) * dx + (start.Y - BattleSpace.Origin.Y) * dy);
            var end = new Point2(start.X + travel * dx, start.Y + travel * dy);

            _pending.Add(new LaserShot(++_telegraphCount, start, end, remaining));
        }

        private void Fire(LaserShot shot, BattlePlayer owner, World world)
        {
            _targets.Clear();
            float halfWidth = Definition.Width / 2;
            IReadOnlyList<Enemy> enemies = world.Enemies;

            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i].IsWithin(NearestOnPath(enemies[i].Position, shot), halfWidth))
                    _targets.Add(enemies[i]);
            }

            var damage = new Damage(Definition.Damage, owner.Id);

            foreach (Enemy target in _targets)
                world.DealDamage(target, damage);

            FireCount++;
            _fires.Add(new LaserFire(shot, Definition.Width, _targets.Count));
        }

        // 경로(선분) 위에서 점에 가장 가까운 점.
        private static Point2 NearestOnPath(Point2 point, LaserShot shot)
        {
            float dx = shot.End.X - shot.Start.X;
            float dy = shot.End.Y - shot.Start.Y;
            float lengthSquared = dx * dx + dy * dy;
            float along = ((point.X - shot.Start.X) * dx + (point.Y - shot.Start.Y) * dy) / lengthSquared;
            along = Math.Max(0, Math.Min(1, along));
            return new Point2(shot.Start.X + along * dx, shot.Start.Y + along * dy);
        }
    }
}
