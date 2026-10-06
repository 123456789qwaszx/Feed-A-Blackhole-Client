namespace BlackHole.Core
{
    // 적 시스템이 공개하는 업그레이드 수치 이름. 노드의 업그레이드(Upgrade.Stat)가 이 이름으로 적 종류의 판 구성을 보정한다.
    // 업그레이드 시스템은 이 이름을 해석하지 않는다. 적 종류마다 이름이 다르다(종류 ID가 들어간다).
    // 특수 성질의 수치는 종류 아래 성질 ID로 나뉜다: enemy.<종류>.trait.<성질>.<수치>.
    public static class EnemyUpgradeStats
    {
        // 질량(%, 기본 100). 색 분포를 정한다(MassRule). 노드는 더하기(Add)만 쓴다 — 예: 더하기 50 = +50%.
        public static string Mass(string kindId) => $"enemy.{kindId}.mass";
        // 전투 시작 공급 수 늘리기. 원작 도전 과제 "50개 이상으로 시작"이 이 강화를 가리킨다. 전체 개체 수 상한 안이어야 한다.
        public static string StartSupply(string kindId) => $"enemy.{kindId}.start-supply";
        // 블랙홀이 Level업할 때마다 이 종류를 더 요청하는 수(원작 "성장 때 소행성 더"). 기본값 0.
        public static string GrowthSupply(string kindId) => $"enemy.{kindId}.growth-supply";
        // 다음 종류로의 변환(원작 "소행성 → 행성 업그레이드", "행성 → 별 업그레이드"). %, 기본값 0.
        public static string Upgrade(string kindId) => $"enemy.{kindId}.upgrade";
        // 픽업(혜성)의 등장 확률: 등장 주기마다 이 확률로 하나가 나온다. %, 기본값 0 — 노드를 사야 나온다. 픽업 종류에만 뜻이 있다.
        public static string Chance(string kindId) => $"enemy.{kindId}.chance";
        // 크기(기본 1, 상한 SizeRule.Max). 크기 1부터 이 값까지가 같은 몫으로 섞여 나온다(SizeRule). 한 노드 = 더하기 1.
        public static string Size(string kindId) => $"enemy.{kindId}.size";
        // 특수 성질의 생성 확률(원작 "전기 소행성 생성 확률", "황금 소행성 추가" 등). %, 기본값 0 — 노드를 사야 붙는다.
        public static string TraitChance(string kindId, string traitId) => $"enemy.{kindId}.trait.{traitId}.chance";
        // 황금 성질의 Gold 배율. 기본값은 그 성질의 배율이다.
        public static string TraitMultiplier(string kindId, string traitId) => $"enemy.{kindId}.trait.{traitId}.multiplier";
        // 황금 성질의 치명타 Gold 배율. 기본값은 그 성질의 배율이다.
        public static string TraitCritRewardScale(string kindId, string traitId) => $"enemy.{kindId}.trait.{traitId}.critRewardScale";
        // 소행성이 파괴될 때 시간이 추가될 확률. %, 기본값 0 - 노드를 사야 시간이 오른다.
        public static string TimeChance(string kindId) => $"enemy.{kindId}.timeChance";
        // 소행성이 파괴될 때 새로운 소행성이 생성될 확률. %, 기본값 0 - 노드를 사야 재소환된다. 상한 100%.
        public static string RespawnChance(string kindId) => $"enemy.{kindId}.respawnChance";
        public static string TraitDamage(string kindId, string traitId) => $"enemy.{kindId}.trait.{traitId}.damage";
        public static string TraitChain(string kindId, string traitId) => $"enemy.{kindId}.trait.{traitId}.chain";
        // 전기 소행성이 갈라질 확률. %, 기본값 0 - 노드를 사야 전기 갈라짐이 발생한다.
        public static string TraitSplitChance(string kindId, string traitId) => $"enemy.{kindId}.trait.{traitId}.splitChance";
        // 특수 성질의 행성의 치명타 확률. %, 기본값 0 - 노드를 사야 치명타가 발생한다.
        public static string TraitCritChance(string kindId, string traitId) => $"enemy.{kindId}.trait.{traitId}.critChance";
        // 전기 소행성의 치명타 배율 . %, 기본값 0 - 노드를 사야 치명타 배율이 증가한다.
        public static string TraitCritBonus(string kindId, string traitId) => $"enemy.{kindId}.trait.{traitId}.critBonus";
    }
}
