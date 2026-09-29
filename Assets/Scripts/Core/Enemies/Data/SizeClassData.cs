using System;

namespace BlackHole.Core
{
    // 크기 등급 한 줄: 색 등급의 크기·HP·Gold·EXP에 곱하는 계수.
    [Serializable]
    public sealed class SizeClassData
    {
        public float SizeMultiplier;
        public float HealthMultiplier;
        public float GoldMultiplier;
        public float ExpMultiplier;
    }
}
