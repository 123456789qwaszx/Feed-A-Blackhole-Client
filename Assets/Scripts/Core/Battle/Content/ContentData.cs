using System;

namespace BlackHole.Core
{
    // 판 콘텐츠의 저작 형식. 검증 전 값이며 실행에 쓰지 않는다 — ContentLoader만 읽는다.
    // 적은 적 에셋(EnemyCatalog, EnemySupplySetup)이, 스킬은 스킬 설정 에셋이, 블랙홀 성장의 Level 표는 블랙홀 성장 에셋이 채운다.
    [Serializable]
    public sealed class ContentData
    {
        public SessionData Session;
        // 스킬은 종류마다 칸이 따로 있다. 비어 있으면 판에 그 스킬이 없다.
        public BreakerData Breaker;
        public LaserData Laser;
        // 적 종류, 출현 배치, 전체 개체 수 상한, 전투 시작 공급.
        public EnemyContentData Enemies = new EnemyContentData();
        // 블랙홀 성장: 성장도마다의 판 Level 표와 이정표. 없으면 블랙홀이 Level 0·성장도 0에 머문다.
        public HqGrowthData Growth;
    }
}
