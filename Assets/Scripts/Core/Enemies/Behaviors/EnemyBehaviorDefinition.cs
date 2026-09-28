namespace BlackHole.Core
{
    // 행동 종류의 정의. 적 본체는 이것이 어떤 행동인지 모른다.
    // 새 행동: 하위 정의 + 행동 구현(IEnemyBehavior) + EnemyBehaviors.Create 분기 + ContentLoader의 종류 이름.
    public abstract class EnemyBehaviorDefinition
    {
        private protected EnemyBehaviorDefinition() { }
    }
}
