using System;

namespace BlackHole.Core
{
    [Serializable]
    public sealed class LaserData
    {
        public float Damage;
        // 예고를 시작하는 주기(초).
        public float Interval;
        // 발사선의 굵기.
        public float Width;
        // 예고가 보이는 시간(초).
        public float TelegraphDuration;
        // 시작점이 놓이는 경계 원의 반지름(HQ 중심).
        public float BoundaryRadius;
    }
}
