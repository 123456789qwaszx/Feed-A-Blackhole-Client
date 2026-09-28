namespace BlackHole.Core
{
    public enum PurchaseResult
    {
        Purchased,
        // 요청한 노드 ID가 트리에 없다.
        UnknownNode,
        AlreadyOwned,
        // 아직 드러나지 않았다: 시작 노드가 아니고 산 이웃도 없다.
        Hidden,
        NotEnoughGold,
    }
}
