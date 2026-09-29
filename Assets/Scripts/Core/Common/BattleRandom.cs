namespace BlackHole.Core
{
    // 한 전투의 난수. 판 조립 때 seed로 만들고 판이 끝나면 버린다.
    // seed와 입력과 진행 시간이 같으면 같은 값이 같은 순서로 나온다(기준 상황 재현, S12).
    // 엔진의 난수를 쓰지 않는다. 용도마다 스트림이 다르다: 한 용도가 난수를 더 쓰거나 덜 써도 다른 용도의 순서는 그대로다
    // (예: 황금 비율을 바꿔도 출현 위치가 같고, 치명타 확률을 바꿔도 레이저 시작점이 같다).
    internal sealed class BattleRandom
    {
        // 용도의 번호. 판에 하나인 용도(World)와 참가자마다의 용도(BattlePlayer)가 있다.
        public const int PlacementStream = 0;
        public const int TierStream = 1;
        public const int GoldenStream = 2;
        public const int LaserStream = 3;
        public const int CriticalStream = 4;
        public const int KindStream = 5;
        public const int SizeStream = 6;

        private uint _state;

        public BattleRandom(int seed)
        {
            _state = unchecked((uint)seed);
        }

        // 같은 seed에서 용도가 다른 난수. 번호 0은 seed만 받는 생성자와 같다.
        public BattleRandom(int seed, int stream)
        {
            _state = unchecked((uint)seed ^ ((uint)stream * 0x85EBCA6Bu));
        }

        // 판 seed에서 한 참가자의 한 용도만 쓰는 난수. 참가자끼리도 순서가 서로 흔들리지 않는다.
        public static BattleRandom ForPlayer(int seed, int stream, PlayerId player) =>
            new BattleRandom(unchecked((int)((uint)seed ^ ((uint)player.Value * 0x9E3779B1u))), stream);

        // [0, 1) 구간의 값. 32비트 SplitMix 방식이라 플랫폼과 런타임에 관계없이 같다.
        public float NextFloat()
        {
            unchecked
            {
                _state += 0x9E3779B9u;
                uint z = _state;
                z = (z ^ (z >> 16)) * 0x85EBCA6Bu;
                z = (z ^ (z >> 13)) * 0xC2B2AE35u;
                z ^= z >> 16;
                return (z >> 8) * (1f / 16777216f);
            }
        }
    }
}
