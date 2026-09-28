namespace BlackHole.Core
{
    // Preparing: 조립이 끝났다(적 수치를 확정했다). 아직 적이 없고 시간이 흐르지 않는다.
    // Running / Paused: Begin 뒤. Ended: 결과가 확정됐다.
    public enum SessionPhase { Preparing, Running, Paused, Ended }
}
