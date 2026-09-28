using System;

namespace BlackHole.Core
{
    // 이정표 하나: 성장도(성장도 표 안, 1 이상)와 고정 보상(Gold, 0 이상).
    [Serializable]
    public sealed class HqMilestoneData
    {
        public int Stage;
        public long Reward;
    }
}
