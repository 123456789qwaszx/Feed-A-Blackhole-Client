using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    [Serializable]
    public sealed class NodeData
    {
        // 저장 키
        public string Id;

        public long Price;

        // 시작 노드(상시 노출)
        public bool Start;

        // 격자 칸. (한 칸에 노드 하나)
        public int X;
        public int Y;

        // 선으로 이어진 노드의 ID. 선은 방향이 없어 한쪽 노드에만 적어도 됨.
        public List<string> Links = new();

        // 이 노드를 사면 받는 업그레이드.
        public List<UpgradeData> Upgrades = new();
    }
}
