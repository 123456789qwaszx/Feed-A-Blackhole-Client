namespace BlackHole.Core
{
    // 판이 공개하는 업그레이드 수치 이름. 노드의 업그레이드(Upgrade.Stat)가 이 이름으로 판의 제한 시간을 보정한다.
    // 업그레이드 시스템은 이 이름을 해석하지 않는다.
    public static class SessionUpgradeStats
    {
        // 판의 제한 시간(초). 기본값은 콘텐츠의 Session.TimeLimit이다(원작 "세션 타이머"). 노드 예: 더하기 2.
        public const string TimeLimit = "session.time-limit";
    }
}
