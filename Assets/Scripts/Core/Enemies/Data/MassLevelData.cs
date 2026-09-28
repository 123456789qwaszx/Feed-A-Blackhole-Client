using System;

namespace BlackHole.Core
{
    // 질량 단계 한 줄: 색 등급 표에 곱하는 HP·Gold 계수.
    [Serializable]
    public sealed class MassLevelData
    {
        public float HealthMultiplier;
        public float GoldMultiplier;
    }
}
