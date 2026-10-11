using System;

namespace BlackHole.Core
{
    // 중력 굴절광(별 등): 특수 적이 죽은 자리에서 빛무리가 블랙홀 반대쪽으로 뻗어 나가다가, 포물선을 그리며 꺾여 블랙홀(원점)로 빨려 들어간다.
    // - 경로는 죽은 자리 → (바깥쪽으로 비스듬히 Length만큼 나간 조절점) → 원점의 2차 베지어 곡선이다. 원점에 가까울수록 빨라진다.
    // - 빛무리의 머리가 지나간 자리의 너비 안에 닿은 적에게 피해를 준다. 빛무리 하나는 같은 적을 한 번만 맞힌다(관통).
    // - Count개의 빛무리가 서로 다른 곡선으로 나간다. 빛무리가 여러 개면 같은 적이 여러 번 맞을 수 있다.
    // 피해는 특수 적에게 가지 않는다(DeathEffects).
    public sealed class LensingLightDefinition : DeathEffectDefinition
    {
        // 노드 저작 실수를 막는 상한.
        public const int MaxCount = 16;

        public override DeathEffectType Type => DeathEffectType.LensingLight;

        public float Damage { get; }
        public float Width { get; }  // 빛무리의 판정 너비(경로 양쪽 거리의 합).
        public float Length { get; } // 바깥쪽으로 뻗는 길이(곡선 조절점까지의 거리).
        public int Count { get; }    // 한 번에 나가는 빛무리 수.

        public LensingLightDefinition(float damage, float width, float length, int count)
        {
            Damage = DefinitionGuard.Positive(damage, nameof(damage));
            Width = DefinitionGuard.Positive(width, nameof(width));
            Length = DefinitionGuard.Positive(length, nameof(length));

            if (count < 1 || count > MaxCount)
                throw new ArgumentOutOfRangeException(nameof(count), $"빛무리 수는 1부터 {MaxCount}까지다. 받은 값: {count}.");

            Count = count;
        }
    }
}
