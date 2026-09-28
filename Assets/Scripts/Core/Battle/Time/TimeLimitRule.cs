using System;

namespace BlackHole.Core
{
    // 한 판의 시간제 종료 판정. 판마다 하나 있다. 경과 시간은 GameSession이 가진다.
    // 진행 전 LimitStep, 진행 후 HasExpired, 남은 시간 표시가 모두 같은 Limit을 본다.
    // 제한 시간은 판의 것이다: 블랙홀 성장이 이 판에서만 늘린다(Extend). 공유 정의는 바뀌지 않아 다음 판은 다시 기본값에서 시작한다.
    public sealed class TimeLimitRule
    {
        public TimeLimitDefinition Definition { get; }
        // 이 판의 제한 시간(초).
        public float Limit { get; private set; }

        internal TimeLimitRule(TimeLimitDefinition definition)
        {
            Definition = definition;
            Limit = definition.Duration;
        }

        public float Remaining(float elapsed) => Math.Max(0, Limit - elapsed);

        // 진행 전: 이번 진행이 제한 시간을 넘지 않게 자른다.
        internal float LimitStep(float elapsed, float step) => Math.Min(step, Limit - elapsed);

        // 진행 후: 지금까지의 진행으로 판의 종료를 판정한다.
        internal bool HasExpired(float elapsed) => elapsed >= Limit;

        // 이 판의 제한 시간을 늘린다(블랙홀 Level업, BLACKHOLE_GROWTH_PLAN 4.3). 종료 판정 전에 부른다.
        internal void Extend(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds), "0 이상의 유한한 값이 필요하다.");

            Limit += seconds;
        }
    }
}
