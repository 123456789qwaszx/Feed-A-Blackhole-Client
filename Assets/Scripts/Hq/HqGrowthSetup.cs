using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 아래 값은 모두 [임시]
    // 블랙홀 성장 설정 에셋.
    [CreateAssetMenu(fileName = "HqGrowthSetup", menuName = "BlackHole/Hq Growth Setup")]
    public sealed class HqGrowthSetup : ScriptableObject
    {
        [Serializable]
        public struct Stage
        {
            [Tooltip("이 판에서 Level 1, 2, …에 닿는 누적 EXP. 앞 줄보다 커야 한다. 표 끝에서는 EXP만 쌓인다.")]
            public List<long> levelExp;
            [Tooltip("이 판에서 이 Level에 닿으면 결산 때 성장도가 1 오른다. 1 이상, 표 안. 마지막 성장도만 0(목표 없음)일 수 있다.")]
            public int goalLevel;
        }

        [Serializable]
        public struct Milestone
        {
            [Tooltip("이 성장도(1 이상, 성장도 표 안)에 닿으면 받는다. 앞 이정표보다 커야 한다.")]
            public int stage;
            [Tooltip("결산이 그 판에서 번 Gold 대신 주는 고정 보상.")]
            public long reward;
        }

        [Header("성장도 표 (줄 번호 = 성장도, 0부터)")]
        [SerializeField] private List<Stage> stages = new List<Stage>();

        [Header("이정표 (성장도가 커지는 순서)")]
        [SerializeField] private List<Milestone> milestones = new List<Milestone>();

        public HqGrowthData ToData()
        {
            var data = new HqGrowthData();

            foreach (Stage stage in stages)
            {
                data.Stages.Add(new GrowthStageData
                {
                    LevelExp = stage.levelExp != null ? new List<long>(stage.levelExp) : new List<long>(),
                    GoalLevel = stage.goalLevel,
                });
            }

            foreach (Milestone milestone in milestones)
                data.Milestones.Add(new HqMilestoneData { Stage = milestone.stage, Reward = milestone.reward });

            return data;
        }
    }
}
