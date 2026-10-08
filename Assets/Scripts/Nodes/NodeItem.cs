namespace BlackHole.Unity
{
    // 업그레이드 화면(NodeTreeView)에 노드 하나를 그리는 데 필요한 것.
    // 콘텐츠 로드 때 노드 배치(칸)와 노드 정의(가격·그림 스탯·Rank)로 한 번 만든다(GameContentLoader).
    public readonly struct NodeItem
    {
        public string Id { get; }
        public int X { get; }
        public int Y { get; }
        // 처음 그릴 때의 가격(Rank 1 비용). 이후에는 Show가 다음 Rank 비용으로 바꾼다.
        public long Price { get; }
        // 노드 그림을 고르는 스탯 키(Rank 1 첫 효과의 StatId). 없으면 기본 그림.
        public string Stat { get; }
        // 최대 Rank. 둘 이상이면 Rank 딱지를 단다.
        public int MaxRank { get; }

        public NodeItem(string id, int x, int y, long price, string stat, int maxRank)
        {
            Id = id;
            X = x;
            Y = y;
            Price = price;
            Stat = stat;
            MaxRank = maxRank;
        }
    }
}
