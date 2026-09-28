using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 블랙홀 성장도별 색 비율 한 줄:
    // - 이 성장도부터 그 종류의 색이 특정 비율로 나온다는 의미.
    public sealed class StageColorDefinition
    {
        // 이 줄을 쓰기 시작하는 성장도(0 이상).
        public int FromStage { get; }

        // 색 등급 표와 같은 순서·길이. 0 이상이고 합이 0보다 크다. 합이 1이 아니어도 된다(비율로 읽는다).
        public IReadOnlyList<float> TierRatios { get; }

        public StageColorDefinition(int fromStage, IReadOnlyList<float> tierRatios)
        {
            if (fromStage < HqGrowthDefinition.StartStage)
                throw new ArgumentOutOfRangeException(nameof(fromStage), $"{HqGrowthDefinition.StartStage} 이상이 필요하다.");

            if (tierRatios == null || tierRatios.Count == 0)
                throw new ArgumentException("색 비율이 하나 이상 필요하다.", nameof(tierRatios));

            var copy = new float[tierRatios.Count];
            float sum = 0;

            for (int i = 0; i < copy.Length; i++)
            {
                float ratio = tierRatios[i];

                if (float.IsNaN(ratio) || float.IsInfinity(ratio) || ratio < 0)
                    throw new ArgumentOutOfRangeException(nameof(tierRatios), $"{i}번 색의 비율은 0 이상의 유한한 값이어야 한다.");

                copy[i] = ratio;
                sum += ratio;
            }

            if (sum <= 0)
                throw new ArgumentException("색 비율의 합이 0보다 커야 한다.", nameof(tierRatios));

            FromStage = fromStage;
            TierRatios = Array.AsReadOnly(copy);
        }
    }
}
