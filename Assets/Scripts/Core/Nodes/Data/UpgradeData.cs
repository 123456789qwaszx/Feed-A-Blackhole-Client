using System;

namespace BlackHole.Core
{
    [Serializable]
    public sealed class UpgradeData
    {
        // 스탯의 이름만을 제공.
        // 그것의 역할은 가져가는 시스템에 의해 결정.
        public string Stat;

        public UpgradeOperation Operation;

        public float Value;
    }
}
