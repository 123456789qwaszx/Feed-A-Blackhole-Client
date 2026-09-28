namespace BlackHole.Core
{
    // HQ 주위를 돈다. 지금 게임에 있는 유일한 행동이다(GAME_RULES 7절).
    public sealed class OrbitBehaviorDefinition : EnemyBehaviorDefinition
    {
        public bool Clockwise { get; }

        public OrbitBehaviorDefinition(bool clockwise)
        {
            Clockwise = clockwise;
        }
    }
}
