namespace BlackHole.Core
{
    // 적 행동의 경계. 적 본체는 이 인터페이스만 안다. 위치의 원본은 Enemy이고, 행동은 다음 위치만 정한다.
    // 상태 기계나 행동 전환은 만들지 않는다(GAME_RULES 7절). 실제 기획이 생길 때 도입한다.
    internal interface IEnemyBehavior
    {
        Point2 NextPosition(Point2 position, EnemyStats stats, float delta);
    }
}
