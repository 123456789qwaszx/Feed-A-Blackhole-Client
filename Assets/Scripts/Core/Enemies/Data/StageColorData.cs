using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 블랙홀 성장도별 색 비율 한 줄: FromStage부터 색마다 나오는 비율(색 등급 표와 같은 순서·길이).
    [Serializable]
    public sealed class StageColorData
    {
        public int FromStage;
        public List<float> TierRatios = new List<float>();
    }
}
