using System;

namespace BlackHole.Core
{
    [Serializable]
    public sealed class SessionData
    {
        // 시간제 종료(현재 후보). 초 단위.
        public float TimeLimit;
    }
}
