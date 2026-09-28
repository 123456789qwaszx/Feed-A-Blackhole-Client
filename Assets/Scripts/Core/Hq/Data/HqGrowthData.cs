using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 블랙홀 성장: Stages[s]가 성장도 s의 판에서 쓰는 Level 표와 목표 Level이다. 성장 효과는 노드가 정한다.
    [Serializable]
    public sealed class HqGrowthData
    {
        public List<GrowthStageData> Stages = new List<GrowthStageData>();
        // 이정표(성장도가 커지는 순서). 그 앞 성장도의 판이 목표 Level에 닿으면 판이 끝나고 결산이 번 Gold 대신 보상을 준다.
        public List<HqMilestoneData> Milestones = new List<HqMilestoneData>();
    }
}
