using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // HqGrowthData(저작 형식) → HqGrowthDefinition(검증된 정의).
    // 데이터가 없으면 HqGrowthDefinition.None이다(블랙홀이 Level 0·성장도 0에 머문다).
    // 오류가 하나라도 있으면 null을 돌려주고, 모든 진단을 into에 더한다(부분 통과 금지). 진단 경로는 "Growth."로 시작한다.
    public static class HqGrowthLoader
    {
        public static HqGrowthDefinition Load(HqGrowthData item, List<ContentDiagnostic> into)
        {
            if (into == null)
                throw new ArgumentNullException(nameof(into));

            if (item == null)
                return HqGrowthDefinition.None;

            var stages = new List<GrowthStageDefinition>();
            var milestones = new List<HqMilestone>();
            int errors = into.Count;

            for (int i = 0; item.Stages != null && i < item.Stages.Count; i++)
            {
                GrowthStageData stage = item.Stages[i];

                if (stage == null)
                {
                    into.Add(new ContentDiagnostic($"Growth.Stages[{i}]", "데이터가 없다."));
                    continue;
                }

                // 표의 오류는 LevelExp에, 목표 Level이 표 밖인 것은 GoalLevel에 붙인다.
                try
                {
                    stages.Add(new GrowthStageDefinition(stage.LevelExp ?? new List<long>(), stage.GoalLevel));
                }
                catch (ArgumentException error)
                {
                    string field = error.ParamName == "goalLevel" ? "GoalLevel" : "LevelExp";
                    into.Add(new ContentDiagnostic($"Growth.Stages[{i}].{field}", error.Message));
                }
            }

            for (int i = 0; item.Milestones != null && i < item.Milestones.Count; i++)
            {
                HqMilestoneData mark = item.Milestones[i];

                if (mark == null)
                {
                    into.Add(new ContentDiagnostic($"Growth.Milestones[{i}]", "데이터가 없다."));
                    continue;
                }

                try
                {
                    milestones.Add(new HqMilestone(mark.Stage, mark.Reward));
                }
                catch (ArgumentException error)
                {
                    into.Add(new ContentDiagnostic($"Growth.Milestones[{i}]", error.Message));
                }
            }

            if (into.Count > errors)
                return null;

            // 목표 없는 성장도가 마지막이 아닌 것은 Stages에, 이정표가 성장도 표 밖·순서가 틀린 것은 Milestones에 붙인다.
            try
            {
                return new HqGrowthDefinition(stages, milestones);
            }
            catch (ArgumentException error)
            {
                into.Add(new ContentDiagnostic(error.ParamName == "milestones" ? "Growth.Milestones" : "Growth.Stages", error.Message));
                return null;
            }
        }
    }
}
