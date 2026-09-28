namespace BlackHole.Core
{
    // 판이 끝날 때 한 번 확정되는 결과. 이후 판 상태가 바뀌어도 변하지 않는 스냅샷이다.
    public sealed class SessionResult
    {
        public float PlayedSeconds { get; }

        internal SessionResult(float playedSeconds)
        {
            PlayedSeconds = playedSeconds;
        }
    }
}
