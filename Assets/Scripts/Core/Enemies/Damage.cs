using System;

namespace BlackHole.Core
{
    // 피해 요청 하나. 누가 피해를 줬는지(출처 Player)를 함께 담는다.
    // 출처는 기록일 뿐이다 — 다인 플레이의 처치·보상 귀속 정책은 미정이며, 이 값을 수령자 규칙으로 쓰지 않는다.
    public readonly struct Damage
    {
        public float Amount { get; }
        public PlayerId Source { get; }

        public Damage(float amount, PlayerId source)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "피해량은 유한한 양수여야 한다.");

            Amount = amount;
            Source = source;
        }
    }
}
