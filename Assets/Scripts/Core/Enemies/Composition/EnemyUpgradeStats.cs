namespace BlackHole.Core
{
    // 적 시스템이 공개하는 업그레이드 수치 이름. 노드의 업그레이드(Upgrade.Stat)가 이 이름으로 적 종류의 판 구성을 보정한다.
    // 업그레이드 시스템은 이 이름을 해석하지 않는다. 적 종류마다 이름이 다르다(종류 ID가 들어간다).
    // 특수 성질의 수치는 종류 아래 성질 ID로 나뉜다: enemy.<종류>.trait.<성질>.<수치>.
    public static class EnemyUpgradeStats
    {
        // 질량(%, 기본 100). 색 분포를 정한다(MassRule). 노드는 더하기(Add)만 쓴다 — 예: 더하기 50 = +50%.
        public static string Mass(string kindId) => $"enemy.{kindId}.mass";
        // 전투 시작 공급 수 늘리기. 원작 도전 과제 "50개 이상으로 시작"이 이 강화를 가리킨다.
        public static string StartSupply(string kindId) => $"enemy.{kindId}.start-supply";
        // 블랙홀이 Level업할 때마다 이 종류를 판 시작 수의 몇 % 요청하는가(원작 "블랙홀 성장 시 생성되는 소행성 수"). %, 기본값 0.
        // 시작 수는 변환까지 반영한 이 판의 전투 시작 공급 수다. 시작 수가 0인 종류는 나오지 않는다.
        public static string GrowthSupply(string kindId) => $"enemy.{kindId}.growth-supply";
        // 판 시작 때 이 종류의 시작 공급 중 몇 마리를 다음 종류로 바꾸는가(원작 "소행성을 행성으로 업그레이드"). 마리 수, 기본값 0.
        // 시작 공급보다 많으면 시작 공급만큼만 바뀐다. 사슬 앞쪽부터 바꾼다(소행성 → 행성을 먼저, 그다음 행성 → 별).
        public static string Upgrade(string kindId) => $"enemy.{kindId}.upgrade";
        // 주기 출현 종류(혜성)의 등장 확률: 출현 주기마다 이 확률로 하나가 나온다. %, 기본값 0 — 노드를 사야 나온다. 주기 출현 종류에만 뜻이 있다.
        // 수치 이름은 enemy.<종류>.chance 그대로 둔다(GUI 아이콘·매핑이 이 이름을 쓴다).
        public static string SpawnChance(string kindId) => $"enemy.{kindId}.chance";
        // 픽업(혜성)이 나올 때 혜성 비가 될 확률(원작 "혜성이 내릴 확률"). 혜성 비면 종류의 혜성 비 수(EnemyDefinition.RainCount)만큼 한꺼번에 나온다.
        // %, 기본값 0. 픽업 종류에만 뜻이 있다.
        public static string RainChance(string kindId) => $"enemy.{kindId}.rain-chance";
        // 이 종류가 파괴될 때 같은 종류를 하나 새로 요청할 확률(원작 "행성이 파괴될 때 새로운 행성을 생성할 확률"). %, 기본값 0.
        public static string RespawnChance(string kindId) => $"enemy.{kindId}.respawn-chance";
        // 이 종류가 파괴될 때 판의 제한 시간이 늘어날 확률(원작 "행성이 파괴될 때 시간이 추가될 확률"). 늘어나는 초는 판 설정(TimeLimitDefinition.KillTimeBonus).
        // %, 기본값 0.
        public static string TimeChance(string kindId) => $"enemy.{kindId}.time-chance";
        // 크기(기본 1, 상한 SizeRule.Max). 크기 1부터 이 값까지가 같은 몫으로 섞여 나온다(SizeRule). 한 노드 = 더하기 1.
        public static string Size(string kindId) => $"enemy.{kindId}.size";
        // 특수 성질의 생성 확률(원작 "전기 소행성 생성 확률", "황금 소행성 추가" 등). %, 기본값 0 — 노드를 사야 붙는다.
        public static string TraitChance(string kindId, string traitId) => $"enemy.{kindId}.trait.{traitId}.chance";
        // 황금 성질의 Gold 배율. 기본값은 그 성질의 배율이다.
        public static string TraitMultiplier(string kindId, string traitId) => $"enemy.{kindId}.trait.{traitId}.multiplier";
        // 이 성질의 동시 상한: 살아 있는 그 성질 적 + Breaker에 남은 그 버프 중첩(원작 "달 최대 개수"). 기본값은 그 성질의 값(EnemyTraitDefinition.MaxActive)이다.
        public static string TraitMaxActive(string kindId, string traitId) => $"enemy.{kindId}.trait.{traitId}.max-active";

        // 성질 사망 효과의 수치. 기본값은 그 성질 효과의 값이다(적 종류 에셋). 효과에 없는 수치는 무시된다.
        // 피해(번개·레이저).
        public static string TraitDamage(string kindId, string traitId) => $"enemy.{kindId}.trait.{traitId}.damage";
        // 치명타 확률(번개·레이저, 0 ~ 1, 1을 넘지 않는다).
        public static string TraitCritChance(string kindId, string traitId) => $"enemy.{kindId}.trait.{traitId}.crit-chance";
        // 치명타 피해 배율(번개·레이저, 2 = ×2 = 시트 200%).
        public static string TraitCritMultiplier(string kindId, string traitId) => $"enemy.{kindId}.trait.{traitId}.crit-multiplier";
        // 번개 한 줄기가 옮겨 가는 최대 횟수(원작 "최대 연쇄").
        public static string TraitMaxTargets(string kindId, string traitId) => $"enemy.{kindId}.trait.{traitId}.max-targets";
        // 번개 줄기가 하나 더 나갈 확률(원작 "갈라질 확률", 0 ~ 1, 1을 넘지 않는다).
        public static string TraitBranchChance(string kindId, string traitId) => $"enemy.{kindId}.trait.{traitId}.branch-chance";
        // 레이저 너비.
        public static string TraitWidth(string kindId, string traitId) => $"enemy.{kindId}.trait.{traitId}.width";
        // 번개가 한 번 옮겨 가는 최대 거리 / 폭발 반지름.
        public static string TraitRadius(string kindId, string traitId) => $"enemy.{kindId}.trait.{traitId}.radius";
        // 폭발 피해 = 대상 현재 HP × 이 비율(0 ~ 1, 1을 넘지 않는다).
        public static string TraitHealthFraction(string kindId, string traitId) => $"enemy.{kindId}.trait.{traitId}.health-fraction";
        // 황금 성질의 치명타 Gold 배율(1 = 기본 Gold의 100%를 더 얹음 = 시트 100%). 기본값은 그 성질의 값이다.
        public static string TraitCritRewardScale(string kindId, string traitId) => $"enemy.{kindId}.trait.{traitId}.crit-reward-scale";
    }
}
