using System;

namespace BlackHole.Core
{
    // 이정표 하나: 판이 이 Level에 닿으면 그 Step에서 판이 끝나고 성장도가 1 오른다.
    // 보상은 고정 금액이 아니라 목표 잔액이다: 결산이 진행 상태의 Gold가 TargetGold가 되도록 차액을 준다(그 판에서 번 Gold는 버린다).
    // 소지금이 이미 더 많으면 깎지 않는다(차액 0). 금액은 기획자가 정한다 [사용자].
    public sealed class HqMilestone
    {
        public int Level { get; }
        public long TargetGold { get; }

        public HqMilestone(int level, long targetGold)
        {
            if (level <= HqGrowthDefinition.StartLevel)
                throw new ArgumentOutOfRangeException(nameof(level), $"이정표 Level은 {HqGrowthDefinition.StartLevel + 1} 이상이어야 한다. 받은 값: {level}.");

            Level = level;
            TargetGold = DefinitionGuard.NotNegative(targetGold, nameof(targetGold));
        }

        // 진행 상태의 Gold가 gold일 때 결산이 더하는 Gold.
        public long RewardFor(long gold) => Math.Max(0, TargetGold - gold);
    }
}
