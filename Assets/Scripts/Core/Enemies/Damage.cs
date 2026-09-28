using System;

namespace BlackHole.Core
{
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
