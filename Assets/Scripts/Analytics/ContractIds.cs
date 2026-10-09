namespace BlackHole.Analytics
{
    // 계약의 ID(enemyId·traitId) 규칙: 게임 열거형(EnemyType·EnemyTraitType)의 이름에서 첫 글자만 소문자로 쓴다.
    // Asteroid → asteroid, Supernova → supernova. 두 단어 이름이 생기면 GoldenAsteroid → goldenAsteroid다.
    // 계약은 게임 Core를 모르므로 열거형 값이 아니라 이름(ToString())을 받는다.
    public static class ContractIds
    {
        public static string Of(string typeName) =>
            char.ToLowerInvariant(typeName[0]) + typeName.Substring(1);
    }
}
