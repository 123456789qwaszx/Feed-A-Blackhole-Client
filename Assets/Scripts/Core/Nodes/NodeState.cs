namespace BlackHole.Core
{
    public enum NodeState
    {
        Hidden,      // 시작 노드가 아니고 산 이웃도 없음.
        Revealed,    // 보이지만 지금은 살 수 없다(Gold 부족).
        Purchasable,
        Owned,
    }
}
