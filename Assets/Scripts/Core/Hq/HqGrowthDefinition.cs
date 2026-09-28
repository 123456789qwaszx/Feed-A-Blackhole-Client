using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 블랙홀 성장의 공유 정의:
    // 1. 성장도마다의 판 Level 표와
    // 2. 목표 Level
    // 3. 이정표(성장도 10, 20, 30)
    public sealed class HqGrowthDefinition
    {
        public const int StartStage = 0;

        public static readonly HqGrowthDefinition None = new(Array.Empty<GrowthStageDefinition>());

        public IReadOnlyList<GrowthStageDefinition> Stages { get; }
        public int MaxStage => Math.Max(StartStage, Stages.Count - 1);

        // 이정표(성장도가 커지는 순서). 한 성장도에 하나다.
        public IReadOnlyList<HqMilestone> Milestones { get; }

        public HqGrowthDefinition(
            IReadOnlyList<GrowthStageDefinition> stages,
            IReadOnlyList<HqMilestone> milestones = null)
        {
            if (stages == null)
                throw new ArgumentNullException(nameof(stages));

            var rows = new GrowthStageDefinition[stages.Count];

            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = stages[i] ?? throw new ArgumentException($"성장도 {i}의 표가 null이다.", nameof(stages));

                // 마지막 성장도가 아니면 다음 성장도로 가는 목표가 있어야 한다. 목표 0은 그 성장도에서 멈춘다는 뜻이다.
                if (i < rows.Length - 1 && rows[i].GoalLevel == GrowthStageDefinition.NoGoal)
                    throw new ArgumentException($"성장도 {i}의 목표 Level이 0이다. 마지막이 아닌 성장도는 목표 Level이 1 이상이어야 한다.", nameof(stages));
            }

            Stages = Array.AsReadOnly(rows);

            var marks = milestones != null
                ? new HqMilestone[milestones.Count]
                : Array.Empty<HqMilestone>();

            for (int i = 0; i < marks.Length; i++)
            {
                HqMilestone mark =
                    milestones[i] ?? throw new ArgumentException(
                        $"이정표 {i}가 null이다.", nameof(milestones));

                if (mark.Stage <= StartStage || mark.Stage > MaxStage)
                    throw new ArgumentOutOfRangeException(
                        nameof(milestones), $"이정표 {i}의 성장도 {mark.Stage}는 {StartStage + 1}부터" +
                                            $" {MaxStage}까지(성장도 표 안)여야 한다.");

                if (i > 0 && mark.Stage <= marks[i - 1].Stage)
                    throw new ArgumentOutOfRangeException(
                        nameof(milestones), $"이정표 {i}의 성장도 {mark.Stage}는" +
                                            $" 앞 이정표의 {marks[i - 1].Stage}보다 커야 한다.");

                marks[i] = mark;
            }

            Milestones = Array.AsReadOnly(marks);
        }

        // 성장도 stage의 판에서 쓰는 표. 표가 없는 정의(None)는 Level이 오르지 않는 빈 표다.
        public GrowthStageDefinition StageAt(int stage)
        {
            if (stage < StartStage || stage > MaxStage)
                throw new ArgumentOutOfRangeException(nameof(stage), $"성장도는 {StartStage}부터 {MaxStage}까지다. 받은 값: {stage}.");

            return Stages.Count == 0 ? GrowthStageDefinition.Empty : Stages[stage];
        }

        // 이 성장도에 닿으면 받는 이정표. 없으면 null.
        public HqMilestone MilestoneAt(int stage)
        {
            foreach (HqMilestone mark in Milestones)
            {
                if (mark.Stage == stage)
                    return mark;
            }

            return null;
        }

        // 이 성장도 이하인 이정표의 수(이정표 진행도 n / Milestones.Count).
        public int MilestonesReachedBy(int stage)
        {
            int reached = 0;

            foreach (HqMilestone mark in Milestones)
            {
                if (mark.Stage <= stage)
                    reached++;
            }

            return reached;
        }
    }
}
