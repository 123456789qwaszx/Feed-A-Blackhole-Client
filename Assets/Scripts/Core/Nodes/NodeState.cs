namespace BlackHole.Core
{
    // 화면이 노드를 그릴 때 쓰는 상태. 구매 판정(PurchaseResult)을 네 가지로 줄인 것이다.
    public enum NodeState
    {
        // 시작 노드가 아니고 산 이웃도 없다. 실루엣으로라도 보여 줄지는 화면이 정한다.
        Hidden,
        // 보이지만 지금은 살 수 없다(Gold 부족).
        Revealed,
        Purchasable,
        Owned,
    }
}
