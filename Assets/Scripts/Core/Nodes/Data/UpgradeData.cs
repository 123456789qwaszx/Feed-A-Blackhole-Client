using System;

namespace BlackHole.Core
{
    [Serializable]
    public sealed class UpgradeData
    {
        // 수치 이름. 그 수치를 가져가는 시스템이 정한다.
        public string Stat;
        public UpgradeOperation Operation;
        public float Value;
    }
}
