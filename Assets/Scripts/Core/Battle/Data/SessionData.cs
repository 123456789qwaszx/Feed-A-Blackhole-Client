using System;

namespace BlackHole.Core
{
    [Serializable]
    public sealed class SessionData
    {
        // 시간제 종료(현재 후보). 초 단위.
        public float TimeLimit;
        // 적이 파괴될 때 시간 추가(노드 asteroid.timeChance 등)가 성공하면 제한 시간에 더하는 초. 0 이상.
        public float KillTimeBonus;
    }
}
