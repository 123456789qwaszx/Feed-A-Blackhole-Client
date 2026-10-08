using System;

namespace BlackHole.Core
{
    // 판 규칙(BattleRules 에셋)의 저작 형식. ContentLoader가 TimeLimitDefinition으로 검증한다.
    [Serializable]
    public sealed class BattleRulesData
    {
        public float TimeLimit; // 한 판 제한 시간
        public float KillTimeBonus; // 적이 파괴될 때 시간 추가
    }
}
