using System;
using BlackHole.Core;

namespace BlackHole.Simulation
{
    // 비교용 기준 봇: 판을 보지 않고 블랙홀 둘레의 원(반지름 6 × 전장 배율)을 초당 0.7라디안으로 돈다(한 바퀴 약 9초).
    // 적이 나오는 띠(2 ~ 9.6 × 전장 배율)를 일정하게 쓸고 지나간다. 탐욕 봇의 하한선으로 쓴다.
    public sealed class CircleAimBot : IAimBot
    {
        private const float RadiusPerFieldScale = 6f;
        private const float AngularSpeed = 0.7f;

        private float _clock;

        public string Name => "circle";

        public void Begin(int seed) => _clock = 0;

        public Point2? Aim(World world, float delta)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));

            // 이번 프레임의 조준은 지난 프레임까지 흐른 시간으로 정한다(기존 측정과 같은 궤적).
            float angle = _clock * AngularSpeed;
            float radius = RadiusPerFieldScale * world.Hq.FieldScale;
            _clock += delta;
            return new Point2(radius * (float)Math.Cos(angle), radius * (float)Math.Sin(angle));
        }
    }
}
