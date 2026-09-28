namespace BlackHole.Core
{
    // HQ 주위를 돈다. 지금 게임에 있는 유일한 행동이다(GAME_RULES 7절). 움직임 계산은 EnemyBehaviors가 한다.
    public sealed class OrbitBehaviorDefinition
    {
        public bool Clockwise { get; }

        public OrbitBehaviorDefinition(bool clockwise)
        {
            Clockwise = clockwise;
        }
    }
}
