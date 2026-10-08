using System;

namespace BlackHole.Core
{
    public readonly struct Damage
    {
        public float Amount { get; }
        // 피해를 준 스킬·효과. 정산의 스킬별 피해 같은 통계가 이 값으로 나눈다.
        public DamageSource Source { get; }
        public bool IsCritical { get; }

        public Damage(float amount, DamageSource source, bool isCritical = false)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "피해량은 유한한 양수여야 한다.");

            Amount = amount;
            Source = source;
            IsCritical = isCritical;
        }
    }
}
