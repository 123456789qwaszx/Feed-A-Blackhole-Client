using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 성장도 하나의 판 Level 표: LevelExp[i]는 이 판에서 Level (i + 1)에 닿는 누적 EXP(앞 줄보다 커야 한다).
    // GoalLevel: 이 판에서 닿으면 결산 때 성장도가 1 오른다(1 이상, 표 안). 마지막 성장도만 0(목표 없음)일 수 있다.
    [Serializable]
    public sealed class GrowthStageData
    {
        public List<long> LevelExp = new List<long>();
        public int GoalLevel;
    }
}
