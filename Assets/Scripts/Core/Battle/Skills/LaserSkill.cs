using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 참가자 한 명의 관통 레이저 실행 상태. 판마다, 참가자마다 따로 있다(CONTENT_DEFINITION 2.3).
    //
    // 예고(주기마다, World.Step의 2. Passive Attack 자리):
    // 1. 소유 참가자의 지금 조준점을 한 번 읽어 저장한다(Snapshot). 이후 조준점이 움직여도 이 발사의 경로는 그대로다.
    // 2. 경계 원 위의 무작위 지점이 시작점이다. 시작점에서 조준점을 지나 원의 반대편까지가 경로다.
    // 조준점이 없거나 경계 원 안에 있지 않으면 그 주기는 예고 없이 지나간다([임시]). 무작위 값도 뽑지 않는다.
    // 발사(예고가 끝나는 순간): 경로에서 굵기의 절반 안에 있는 살아 있는 적을 먼저 모두 모은 뒤, World.DealDamage로 피해를 준다.
    // 예고가 주기보다 길면 예고가 겹칠 수 있고, 각 예고는 자기 발사 시각에 한 번씩 발사한다.
    // 첫 예고는 판의 첫 Step이다([임시]). 판이 끝나면 Step이 없으므로 예고 중인 발사는 발사하지 않는다.
    public sealed class LaserSkill
    {
        // 진행 시간을 더한 값의 끝자리 오차. 이만큼 모자라도 그 시각에 닿은 것으로 본다.
        private const float TimeEpsilon = 1e-5f;

        private readonly BattleRandom _random;
        private readonly List<LaserShot> _pending = new List<LaserShot>();
        private readonly List<LaserFire> _fires = new List<LaserFire>();
        private readonly List<Enemy> _targets = new List<Enemy>();
        private float _untilNextTelegraph;
        private int _telegraphCount;

        public LaserDefinition Definition { get; }
        // 켜져 있는가. 꺼진 레이저는 예고도 발사도 하지 않는다. 지금 끄고 켜는 곳은 개발용 스킬 콘솔뿐이다(게임 규칙으로 끄는 일은 없다).
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
            float halfWidthSquared = Definition.Width * Definition.Width / 4;
            IReadOnlyList<Enemy> enemies = world.Enemies;

            for (int i = 0; i < enemies.Count; i++)
            {
                if (DistanceSquaredToPath(enemies[i].Position, shot) <= halfWidthSquared)
                    _targets.Add(enemies[i]);
            }

            var damage = new Damage(Definition.Damage, owner.Id);

            foreach (Enemy target in _targets)
                world.DealDamage(target, damage);

            FireCount++;
            _fires.Add(new LaserFire(shot, Definition.Width, _targets.Count));
        }

        // 점에서 경로(선분)까지의 거리의 제곱.
        private static float DistanceSquaredToPath(Point2 point, LaserShot shot)
        {
            float dx = shot.End.X - shot.Start.X;
            float dy = shot.End.Y - shot.Start.Y;
            float lengthSquared = dx * dx + dy * dy;
            float along = ((point.X - shot.Start.X) * dx + (point.Y - shot.Start.Y) * dy) / lengthSquared;
            along = Math.Max(0, Math.Min(1, along));
            return point.DistanceSquared(new Point2(shot.Start.X + along * dx, shot.Start.Y + along * dy));
        }
    }
}
