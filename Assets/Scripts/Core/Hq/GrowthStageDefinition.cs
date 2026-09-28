using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 성장도 하나의 판 Level 표:
    // - LevelExp[i]는 이 판에서 Level (i + 1)에 닿는 누적 EXP(Level 0 = EXP 0에서 센다).
    // - 앞 줄보다 커야 함.
    //
    // 목표 Level:
    // - 이 판에서 여기에 닿으면 결산 때 성장도가 1 오른다. 1 이상,
    // - 표 안(LevelExp.Count 이하)이다. 0이면 목표가 없다.
    public sealed class GrowthStageDefinition
    {
        public const int StartLevel = 0;
        public const int NoGoal = 0;

        internal static readonly GrowthStageDefinition Empty = new(Array.Empty<long>(), NoGoal);

        public IReadOnlyList<long> LevelExp { get; }
        public int MaxLevel => StartLevel + LevelExp.Count;
        public int GoalLevel { get; }

        public GrowthStageDefinition(
            IReadOnlyList<long> levelExp,
            int goalLevel)
        {
            if (levelExp == null)
                throw new ArgumentNullException(nameof(levelExp));

            var copy = new long[levelExp.Count];

            for (int i = 0; i < copy.Length; i++)
            {
                long exp = levelExp[i];

                if (exp <= 0)
                    throw new ArgumentOutOfRangeException(
                        nameof(levelExp), $"{i}번 줄(Level {i + 1})의 누적 EXP는 양수여야 한다.");

                if (i > 0 && exp <= copy[i - 1])
                    throw new ArgumentOutOfRangeException(nameof(levelExp),
                        $"{i}번 줄(Level {i + 1})의 누적 EXP {exp}는 앞 줄의 {copy[i - 1]}보다 커야 한다.");

                copy[i] = exp;
            }

            LevelExp = Array.AsReadOnly(copy);

            if (goalLevel < NoGoal || goalLevel > MaxLevel)
                throw new ArgumentOutOfRangeException(
                    nameof(goalLevel), $"목표 Level은 0부터 {MaxLevel}까지(표 안)여야 한다. 받은 값: {goalLevel}.");

            GoalLevel = goalLevel;
        }

        // 이 판 EXP가 닿는 Level.
        public int LevelAt(long exp)
        {
            int level = StartLevel;

            while (ExpToReach(level + 1) is long next && exp >= next)
                level++;

            return level;
        }

        // Level level에서 이 판 EXP exp일 때, 그 Level의 임계값에서 다음 임계값까지 몇 %인가(0 ~ 1). 마지막 Level이면 1이다.
        public float ProgressAt(int level, long exp)
        {
            long? next = ExpToReach(level + 1);
            long? from = ExpToReach(level);

            if (!next.HasValue || !from.HasValue)
                return 1;

            return (float)Math.Max(0, Math.Min(1, (double)(exp - from.Value) / (next.Value - from.Value)));
        }

        // 이 Level에 닿는 이 판의 누적 EXP. Level 0은 0이다. 표 밖이면 null이다.
        public long? ExpToReach(int level)
        {
            if (level == StartLevel)
                return 0;

            int index = level - StartLevel - 1;
            return index >= 0 && index < LevelExp.Count ? LevelExp[index] : (long?)null;
        }
    }
}
