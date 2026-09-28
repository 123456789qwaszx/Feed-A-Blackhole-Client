namespace BlackHole.Core
{
    // 시간제 종료의 공유 정의. 시간제는 현재 후보이며 최종 종료 조건은 미정이다.
    public sealed class TimeLimitDefinition
    {
        public float Duration { get; }

        public TimeLimitDefinition(float duration)
        {
            Duration = DefinitionGuard.Positive(duration, nameof(duration));
        }
    }
}
