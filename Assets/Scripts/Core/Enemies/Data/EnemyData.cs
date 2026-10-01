using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    [Serializable]
    public sealed class EnemyData
    {
        public string Id;
        public float MoveSpeed;
        // 색 등급 표. 색이 없는 종류는 한 줄이다.
        public List<EnemyTierData> Tiers = new List<EnemyTierData>();
        // 블랙홀 성장도별 색 비율. FromStage가 커지는 순서, 하나 이상.
        public List<StageColorData> StageColors = new List<StageColorData>();
        // 질량 단계 표(HP·Gold 계수). MassLevels[i]가 질량 단계 i다(0 = 질량 증가를 사지 않음). 하나 이상.
        public List<MassLevelData> MassLevels = new List<MassLevelData>();
        // 크기 등급 표. SizeClasses[i]가 크기 등급 i다(0 = 크기 노드를 사지 않아도 나옴). 비어 있으면 크기 등급이 없다(모든 계수 1).
        public List<SizeClassData> SizeClasses = new List<SizeClassData>();
        // 붙을 수 있는 특수 성질(성질 ID 유일). 생성 확률은 노드(enemy.<종류>.trait.<성질>.chance)가 정하고 기본 0%다.
        // 픽업은 정확히 하나이고 언제나 붙는다.
        public List<EnemyTraitData> Traits = new List<EnemyTraitData>();
        // 변환 대상 종류의 ID(소행성 → 행성 → 별). 비어 있으면 변환하지 않는다.
        // 비율은 기본 변환 비율(BaseUpgrade %, 성장도 BaseUpgradeFromStage부터) + 노드(enemy.<종류>.upgrade)다.
        public string UpgradesTo;
        public float BaseUpgrade;
        public int BaseUpgradeFromStage;
        // 픽업의 등장 판정 주기(초). 0이면 공급되는 보통 종류다. 등장 확률은 노드(enemy.<종류>.chance)가 정한다.
        public float PickupPeriod;
    }
}
