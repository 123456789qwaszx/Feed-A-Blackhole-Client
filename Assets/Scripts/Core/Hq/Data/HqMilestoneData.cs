using System;

namespace BlackHole.Core
{
    // 이정표 하나: Level(1 이상, Level 사다리 안)과 목표 잔액(Gold, 0 이상).
    [Serializable]
    public sealed class HqMilestoneData
    {
        public int Level;
        public long TargetGold;
    }
}
