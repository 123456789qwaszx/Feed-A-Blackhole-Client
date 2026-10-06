using System;

namespace BlackHole.Core
{
    // 전투 조립에 필요한 검증된 공유 정의 묶음. 읽기 전용이며 여러 판이 함께 쓴다.
    // 적 콘텐츠의 규칙(ID 유일, 종류 연결, 전체 개체 수 상한, 전투 시작 공급)은 EnemyContent가 생성 때 보장한다.
    // 오류가 있는 콘텐츠의 경로별 보고는 ContentLoader가 맡는다.
    public sealed class GameContent
    {
        public TimeLimitDefinition TimeLimit { get; }
        // 스킬. 없으면 null이고 판에 그 스킬이 없다.
        public BreakerDefinition Breaker { get; }
        public EnemyContent Enemies { get; }
        // 블랙홀 성장: Level 사다리와 이정표. 없으면 HqGrowthDefinition.None(블랙홀이 Level 0·성장도 0에 머문다).
        public HqGrowthDefinition Growth { get; }

        public GameContent(
            TimeLimitDefinition timeLimit,
            BreakerDefinition breaker,
            EnemyContent enemies,
            HqGrowthDefinition growth = null)
        {
            TimeLimit = timeLimit ?? throw new ArgumentNullException(nameof(timeLimit));
            Breaker = breaker;
            Enemies = enemies ?? throw new ArgumentNullException(nameof(enemies));
            Growth = growth ?? HqGrowthDefinition.None;
        }
    }
}
