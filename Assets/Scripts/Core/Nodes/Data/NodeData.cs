using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    [Serializable]
    public sealed class NodeData
    {
        // 저장 키. 정한 뒤에는 바꾸지 않는다.
        public string Id;
        public long Price;
        // 시작 노드: 산 이웃이 없어도 드러난다.
        public bool Start;
        // 격자 칸. X는 오른쪽, Y는 위쪽으로 커진다. 한 칸에 노드 하나(노드 도구가 지킨다).
        public int X;
        public int Y;
        // 선으로 이어진 노드의 ID. 선은 방향이 없어 한쪽 노드에만 적어도 된다.
        public List<string> Links = new List<string>();
        // 이 노드를 사면 받는 업그레이드.
        public List<UpgradeData> Upgrades = new List<UpgradeData>();
    }
}
