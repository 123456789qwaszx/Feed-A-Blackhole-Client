using System;

namespace BlackHole.Core
{
    public sealed class Hq
    {
        public HqGrowthDefinition Growth { get; }

        public int Stage { get; } // 성장도.

        public GrowthStageDefinition StageTable { get; }

        public float GrowthTime { get; } // Level업마다 더하는 보너스 시간.

        public long Exp { get; private set; }

        public int Level { get; private set; } = GrowthStageDefinition.StartLevel;

        public bool IsMaxLevel => Level >= StageTable.MaxLevel;

        public long? NextLevelExp => StageTable.ExpToReach(Level + 1); // 다음 Level까지 필요한 누적 EXP 양.

        public int GoalLevel => StageTable.GoalLevel; // 이번 성장도의 목표 Level. 0이면 목표가 없다(마지막 성장도).

        // 이 판이 목표 Level에 닿았는가. 닿았으면 결산이 성장도를 1 올린다.
        public bool ReachedGoal => GoalLevel != GrowthStageDefinition.NoGoal && Level >= GoalLevel;

        // 결산 뒤의 성장도: 목표에 닿았으면 +1.
        public int NextStage => ReachedGoal && Stage < Growth.MaxStage ? Stage + 1 : Stage;

        // 이 판에서 닿은 이정표. 없으면 null이다.
        public HqMilestone Milestone { get; private set; }

        public bool ReachedMilestone => Milestone != null;

        // 이 판에서 닿은 이정표의 보상. 결산이 번 Gold 대신 이것을 준다.
        public long MilestoneReward => Milestone?.Reward ?? 0;

        // 지금 Level의 임계값에서 다음 임계값까지 몇 %인가(0 ~ 1). 마지막 Level이면 1이다.
        // EXP는 사망 순간에 들고 Level은 Step의 5 자리에서 오르므로, 그 사이에는 1에서 멈춘다.
        public float Progress => StageTable.ProgressAt(Level, Exp);

        // stage: 판을 시작할 때의 성장도(진행 상태의 것). Level 0, EXP 0에서 시작한다.
        internal Hq(
            HqGrowthDefinition growth,
            float growthTime,
            int stage = HqGrowthDefinition.StartStage)
        {
            Growth = growth;

            if (float.IsNaN(growthTime) || float.IsInfinity(growthTime) || growthTime < 0)
                throw new ArgumentOutOfRangeException(nameof(growthTime), "0 이상의 유한한 값이 필요하다.");

            GrowthTime = growthTime;
            StageTable = growth.StageAt(stage);
            Stage = stage;
        }

        internal void AddExp(long exp) => Exp = checked(Exp + exp);

        internal int RaiseLevels()
        {
            int raised = 0;

            while (NextLevelExp is long next && Exp >= next)
            {
                Level++;
                raised++;
            }

            if (Milestone == null && NextStage > Stage)
                Milestone = Growth.MilestoneAt(NextStage);

            return raised;
        }
    }
}
