using System;

namespace BlackHole.Core
{
    // 블랙홀이 공개하는 업그레이드 수치 이름. 적 종류마다의 성장 공급은 EnemyUpgradeStats.GrowthSupply다.
    public static class HqUpgradeStats
    {
        // 판 Level업마다 이 판의 제한 시간에 더하는 초. 기본값 0(성장 노드를 사기 전에는 시간이 늘지 않는다).
        public const string GrowthTime = "hq.growth-time";

        // 업그레이드 표로 판 Level업마다 늘어나는 시간을 계산한다. 음수·무한은 예외다(노드 저작 오류, UpgradeContentCheck가 로드 때 찾는다).
        public static float GrowthTimeFrom(UpgradeTable upgrades)
        {
            if (upgrades == null)
                throw new ArgumentNullException(nameof(upgrades));

            float seconds = upgrades.Apply(GrowthTime, 0);

            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(upgrades), $"Level업마다 늘어나는 시간은 0 이상의 유한한 값이어야 한다. 업그레이드 합: {seconds}.");

            return seconds;
        }
    }
}
