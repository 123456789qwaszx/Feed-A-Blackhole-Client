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
        // 황금일 때 Gold에 곱하는 값. 0이면 황금이 되지 않는다.
        public float GoldenMultiplier;
        // 없거나 종류 이름이 비어 있으면 사망 효과가 없다.
        public DeathEffectData DeathEffect;
        // 변환 대상 종류의 ID(소행성 → 행성 → 별). 비어 있으면 변환하지 않는다.
        // 비율은 기본 변환 비율(BaseUpgrade %, 성장도 BaseUpgradeFromStage부터) + 노드(enemy.<종류>.upgrade)다.
        public string UpgradesTo;
        public float BaseUpgrade;
        public int BaseUpgradeFromStage;
        // 특수 종류이면 부모 종류의 ID. 비어 있으면 특수 종류가 아니다. 생성 확률은 노드(enemy.<종류>.chance)가 정한다.
        public string SpecialOf;
    }
}
