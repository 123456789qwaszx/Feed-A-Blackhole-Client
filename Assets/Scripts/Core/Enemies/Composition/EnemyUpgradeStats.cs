namespace BlackHole.Core
{
    // 적 시스템이 공개하는 업그레이드 수치 이름. 노드의 업그레이드(Upgrade.Stat)가 이 이름으로 적 종류의 판 구성을 보정한다.
    // 업그레이드 시스템은 이 이름을 해석하지 않는다. 적 종류마다 이름이 다르다(종류 ID가 들어간다).
    public static class EnemyUpgradeStats
    {
        // 질량 증가(HP·Gold 계수의 줄 번호). 한 노드 = 더하기 1.
        public static string MassLevel(string kindId) => $"enemy.{kindId}.mass-level";
        // 황금 소행성 추가(더하기)와 행성 노드의 자릿수 올리기(곱하기).
        public static string GoldenRatio(string kindId) => $"enemy.{kindId}.golden-ratio";
        // 황금 배율 올리기. 기본값은 그 종류의 황금 배율이다.
        public static string GoldenMultiplier(string kindId) => $"enemy.{kindId}.golden-multiplier";
        // 전투 시작 공급 수 늘리기. 원작 도전 과제 "50개 이상으로 시작"이 이 강화를 가리킨다. 전체 개체 수 상한 안이어야 한다.
        public static string StartSupply(string kindId) => $"enemy.{kindId}.start-supply";
        // 블랙홀이 Level업할 때마다 이 종류를 더 요청하는 수(원작 "성장 때 소행성 더"). 기본값 0.
        public static string GrowthSupply(string kindId) => $"enemy.{kindId}.growth-supply";
        // 다음 종류로의 변환(원작 "소행성 → 행성 업그레이드", "행성 → 별 업그레이드"). %, 기본값 0.
        public static string Upgrade(string kindId) => $"enemy.{kindId}.upgrade";
        // 특수 종류의 생성 확률(원작 "전기 소행성 생성 확률" 등). %, 기본값 0 — 노드를 사야 나온다.
        public static string Chance(string kindId) => $"enemy.{kindId}.chance";
        // 크기 노드(원작 소행성 크기2·3). 크기 등급 표에서 몇 번째 줄까지 열렸는가. 한 노드 = 더하기 1, 기본값 0(크기 등급 0만 나옴).
        public static string SizeLevel(string kindId) => $"enemy.{kindId}.size-level";
    }
}
