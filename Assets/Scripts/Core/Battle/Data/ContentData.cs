using System;

namespace BlackHole.Core
{
    // 판 콘텐츠의 저작 형식. 검증 전 값이며 실행에 쓰지 않음
    // (ContentLoader만 읽음.) Unity 쪽 게임 콘텐츠 세트(GameContentSetup)가 채운다:
    //
    // - 판 규칙 에셋(BattleRules).
    // - 스킬 설정 에셋.
    // - 적 에셋(EnemyCatalog, EnemySupplySetup).
    // - 블랙홀 성장 에셋.
    [Serializable]
    public sealed class ContentData
    {
        public BattleRulesData BattleRules;

        // Breaker 수치. 필수다.
        public BreakerData Breaker;

        // 적 종류, 출현 배치, 전투 시작 공급.
        public EnemyContentData Enemies = new();

        // 블랙홀 성장: 성장도마다의 판 Level 표와 이정표. 필수다.
        public HqGrowthData Growth;
    }
}
