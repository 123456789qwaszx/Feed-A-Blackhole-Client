using System;

namespace BlackHole.Core
{
    // 크기 규칙: 한 종류의 크기 s(1부터)는 크기 1부터 s까지의 적이 같은 몫으로 섞여 나오게 한다. 종류와 색은 바꾸지 않는다.
    // 크기 k인 적은 색의 베이스 수치에 선형 배율을 받는다:
    // - HP·Gold·EXP: 1 + (k − 1) × 1.0   (크기 2면 2배, 3이면 3배)
    // - 반지름:       1 + (k − 1) × 0.5   (크기 2면 1.5배, 3이면 2배)
    public static class SizeRule
    {
        // 노드를 사지 않은 크기.
        public const int Base = 1;

        // 노드 저작 실수를 막는 상한. 판 조립이 크기마다 수치를 미리 계산하므로 둔다.
        public const int Max = 10;

        private const float StatStep = 1.0f;
        private const float RadiusStep = 0.5f;

        public static float StatMultiplier(int size) => 1 + (Require(size) - Base) * StatStep;

        public static float RadiusMultiplier(int size) => 1 + (Require(size) - Base) * RadiusStep;

        private static int Require(int size)
        {
            if (size < Base || size > Max)
                throw new ArgumentOutOfRangeException(nameof(size), $"크기는 {Base}부터 {Max}까지다. 받은 값: {size}.");

            return size;
        }
    }
}
